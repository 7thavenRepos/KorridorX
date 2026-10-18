using System.Data;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Exceptions;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Models.Payments;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.PaymentChannels;

public sealed class InteracAdminService(AppDbContext db, IProviderWalletResolver connection,
    IBlaaizApiClient provider, IOptions<BlaaizOptions> options)
{
    public async Task<IReadOnlyList<InteracReceivingRouteDto>> ListRoutesAsync(CancellationToken ct) =>
        (await db.Set<InteracReceivingRoute>().AsNoTracking().Where(x => x.Environment == connection.EnvironmentName && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct)).Select(ToDto).ToArray();

    public async Task<object> ProviderRegistrationsAsync(string? status, string? search, int page, CancellationToken ct)
    {
        if (!options.Value.IsEnabled) throw new InvalidOperationException("Blaaiz is disabled; provider registration lookup is unavailable.");
        var response = await provider.ListInteracAutoDepositEmailsAsync(status, search, page, ct);
        var data = response.Data;
        return new { items = data.Data.Select(x => new { id = x.Id, email = x.Email, status = x.Status }).ToArray(),
            page = data.Meta?.CurrentPage > 0 ? data.Meta.CurrentPage : Math.Max(1, page),
            hasMore = data.Meta is not null && data.Meta.CurrentPage < data.Meta.LastPage };
    }

    public async Task<InteracReceivingRouteDto> RegisterAsync(Guid actor, RegisterInteracRouteRequest request, CancellationToken ct)
    {
        var reason = InteracSecurity.Required(request.Reason, 500, "Reason");
        var email = InteracSecurity.NormalizeEmail(request.ReceivingEmail);
        if (!Guid.TryParse(request.ProviderRegistrationId, out var registrationId)) throw new InvalidOperationException("Enter the provider's Interac auto-deposit email registration ID (UUID), not a webhook event ID.");
        if (!request.IndividualEnabled && !request.BusinessEnabled) throw new InvalidOperationException("Enable at least one customer audience.");
        var wallet = await db.ProviderWalletConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProviderWalletConfigurationId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Select a configured CAD provider wallet.");
        if (wallet.ProviderCode != "BLAAIZ" || wallet.Environment != connection.EnvironmentName || wallet.AssetCode != "CAD" || wallet.NetworkCode != "")
            throw new InvalidOperationException("Select a Blaaiz CAD fiat wallet in the current environment.");
        if (await db.Set<InteracReceivingRoute>().AnyAsync(x => x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey &&
            (x.NormalizedReceivingEmail == email || x.ProviderRegistrationId == registrationId.ToString()), ct))
            throw new InvalidOperationException("This receiving email or provider registration already exists. Review the existing route; its historical ownership cannot be replaced.");
        var row = new InteracReceivingRoute { Environment = connection.EnvironmentName, ConnectionKey = connection.ConnectionKey,
            ProviderWalletConfigurationId = wallet.Id, ProviderWalletId = wallet.ProviderWalletId,
            ProviderRegistrationId = registrationId.ToString(), ReceivingEmail = email, NormalizedReceivingEmail = email,
            DisplayName = InteracSecurity.Required(request.DisplayName, 120, "Display name"), IndividualEnabled = request.IndividualEnabled,
            BusinessEnabled = request.BusinessEnabled, CreatedByUserId = actor };
        db.Add(row); InteracSecurity.Audit(db, actor, "ROUTE_DRAFTED", nameof(InteracReceivingRoute), row.Id, reason);
        await db.SaveChangesAsync(ct); return ToDto(row);
    }

    public async Task<InteracReceivingRouteDto> VerifyAsync(Guid actor, Guid id, VerifyInteracRouteRequest request, CancellationToken ct)
    {
        var reason = InteracSecurity.Required(request.Reason, 500, "Reason");
        var row = await CurrentRouteAsync(id, ct); CheckRevision(row, request.Revision);
        if (!options.Value.IsEnabled) throw new InvalidOperationException("Blaaiz is disabled. Keep the existing credentials and integration settings; provider verification is unavailable.");
        var wallet = await db.ProviderWalletConfigurations.AsNoTracking().SingleAsync(x => x.Id == row.ProviderWalletConfigurationId, ct);
        if (wallet.IsDeleted || !wallet.IsActive || !wallet.CollectionEnabled || wallet.VerifiedAt is null ||
            wallet.VerifiedConnectionKey != connection.ConnectionKey || wallet.ProviderWalletId != row.ProviderWalletId || wallet.AssetCode != "CAD")
            throw new InvalidOperationException("Verify and enable this CAD collection wallet for the current provider connection before activating the receiving email.");
        // Read-only provider operations; this service never creates a transfer, collection
        // request, email registration, or changes a provider balance.
        BlaaizApiResult<BlaaizInteracAutoDepositEmailEnvelope> registration;
        BlaaizApiResult<BlaaizWalletData> providerWallet;
        try
        {
            registration = await provider.GetInteracAutoDepositEmailAsync(row.ProviderRegistrationId, ct);
            providerWallet = await provider.GetWalletAsync(row.ProviderWalletId, ct);
        }
        catch (Exception ex) when (ex is ProviderIntegrationException or HttpRequestException)
        {
            row.Status = "VerificationFailed"; row.VerifiedAt = null; row.Revision = Guid.NewGuid(); row.LastUpdatedAt = DateTime.UtcNow;
            row.FailureReason = "Provider revalidation failed. Check the existing registration, CAD wallet and read-scope grant; reverify before sending. Existing credentials were preserved.";
            InteracSecurity.Audit(db, actor, "ROUTE_PROVIDER_REVALIDATION_FAILED", nameof(InteracReceivingRoute), row.Id, row.FailureReason);
            await db.SaveChangesAsync(CancellationToken.None);
            return ToDto(row);
        }
        var problem = ValidateProviderRoute(row, registration.Data.Data, providerWallet.Data);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await InteracSecurity.LockAsync(db, "InteracRoute:" + connection.EnvironmentName + ":" + connection.ConnectionKey, ct);
        await db.Entry(row).ReloadAsync(ct); CheckRevision(row, request.Revision);
        if (problem is null && await db.Set<InteracReceivingRoute>().AnyAsync(x => x.Id != id && x.Environment == connection.EnvironmentName &&
            x.ConnectionKey == connection.ConnectionKey && x.Status == "Active", ct))
            throw new InvalidOperationException("Pause the current active receiving route before activating another. Existing route ownership and receipts are retained.");
        row.ProviderStatus = registration.Data.Data?.Status?.Trim().ToUpperInvariant();
        row.FailureReason = problem; row.Status = problem is null ? "Active" : "VerificationFailed";
        row.VerifiedAt = problem is null ? DateTime.UtcNow : null;
        if (problem is null) row.ProviderBusinessId = providerWallet.Data.BusinessId;
        row.LastUpdatedAt = DateTime.UtcNow; row.LastUpdatedByUserId = actor; row.Revision = Guid.NewGuid();
        InteracSecurity.Audit(db, actor, problem is null ? "ROUTE_PROVIDER_VERIFIED" : "ROUTE_PROVIDER_REJECTED", nameof(InteracReceivingRoute), row.Id,
            problem is null ? reason : reason + " " + problem);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(row);
    }

    public async Task<InteracReceivingRouteDto> StatusAsync(Guid actor, Guid id, SetInteracRouteStatusRequest request, CancellationToken ct)
    {
        if (request.IsEnabled) return await VerifyAsync(actor, id, new(request.Revision, request.Reason), ct);
        var reason = InteracSecurity.Required(request.Reason, 500, "Reason");
        var row = await CurrentRouteAsync(id, ct); CheckRevision(row, request.Revision);
        row.Status = "Paused"; row.Revision = Guid.NewGuid(); row.LastUpdatedAt = DateTime.UtcNow; row.LastUpdatedByUserId = actor;
        InteracSecurity.Audit(db, actor, "ROUTE_PAUSED", nameof(InteracReceivingRoute), id, reason);
        await db.SaveChangesAsync(ct); return ToDto(row);
    }

    // Registration callbacks are hints to disable/recheck availability, never evidence
    // of money received. ACTIVE callbacks cannot bypass the explicit provider GET gate.
    public async Task<bool> ProcessRegistrationEventAsync(string email, string status, CancellationToken ct)
    {
        var normalized = InteracSecurity.OptionalEmail(email); if (normalized is null) return false;
        var row = await db.Set<InteracReceivingRoute>().SingleOrDefaultAsync(x => x.Environment == connection.EnvironmentName &&
            x.ConnectionKey == connection.ConnectionKey && x.NormalizedReceivingEmail == normalized, ct);
        if (row is null) return false;
        if (status.Equals("BLOCKED", StringComparison.OrdinalIgnoreCase))
        {
            row.Status = "Paused"; row.ProviderStatus = "BLOCKED"; row.Revision = Guid.NewGuid();
            row.FailureReason = "The provider reported this receiving email blocked. Reverify before use.";
            InteracSecurity.Audit(db, null, "ROUTE_PROVIDER_BLOCKED", nameof(InteracReceivingRoute), row.Id, row.FailureReason);
        }
        else InteracSecurity.Audit(db, null, "ROUTE_PROVIDER_STATUS_RECEIVED", nameof(InteracReceivingRoute), row.Id,
            "Provider status notification received. Activation still requires explicit registration and CAD-wallet verification.");
        return true;
    }

    public static string? ValidateProviderRoute(InteracReceivingRoute expected, BlaaizInteracAutoDepositEmailData? registration, BlaaizWalletData? wallet)
    {
        if (registration is null || !Guid.TryParse(registration.Id, out var actualId) || !Guid.TryParse(expected.ProviderRegistrationId, out var expectedId) || actualId != expectedId ||
            InteracSecurity.OptionalEmail(registration.Email) != expected.NormalizedReceivingEmail)
            return "The provider did not confirm the exact registered receiving email and registration ID.";
        if (!string.Equals(registration.Status?.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return "The provider registration is not ACTIVE. Complete activation with Blaaiz and reverify.";
        if (wallet is null || wallet.Id != expected.ProviderWalletId || !wallet.IsActive || !wallet.Currency.Equals("CAD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(wallet.BusinessId))
            return "The authenticated provider did not confirm the active CAD wallet and business identity.";
        return null;
    }

    public async Task<InteracDepositPage> DepositsAsync(int page, int pageSize, string? status, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Set<InteracDeposit>().AsNoTracking().Where(x => x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var count = await query.CountAsync(ct); var rows = await query.OrderByDescending(x => x.ReceivedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(DepositDto).ToArray(), count, page, pageSize);
    }

    public async Task<InteracDepositDetailsDto> DepositAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<InteracDeposit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey, ct)
            ?? throw new InvalidOperationException("Interac deposit not found.");
        var notes = await db.Set<InteracDepositReviewNote>().AsNoTracking().Where(x => x.DepositId == id).OrderBy(x => x.CreatedAt)
            .Select(x => new InteracDepositNoteDto(x.Id, x.Note, x.CreatedAt, x.CreatedByUserId)).ToListAsync(ct);
        return new(row.Id, row.ProviderTransactionId, row.ProviderReference, row.Status, row.ReviewReason, row.SenderEmail,
            row.ReceivingEmail, row.CurrencyCode, row.GrossAmount, row.NetAmount, row.FeeAmount, row.FinancialAccountId,
            row.CollectionId, row.ReceivedAt, row.CreditedAt, notes);
    }

    public async Task<InteracDepositDetailsDto> AddNoteAsync(Guid actor, Guid id, string note, CancellationToken ct)
    {
        await DepositAsync(id, ct); var text = InteracSecurity.Required(note, 2000, "Review note");
        db.Add(new InteracDepositReviewNote { DepositId = id, Note = text, CreatedByUserId = actor });
        InteracSecurity.Audit(db, actor, "DEPOSIT_REVIEW_NOTE_ADDED", nameof(InteracDeposit), id, "An append-only review note was added. No monetary action was performed.");
        await db.SaveChangesAsync(ct); return await DepositAsync(id, ct);
    }

    private async Task<InteracReceivingRoute> CurrentRouteAsync(Guid id, CancellationToken ct) =>
        await db.Set<InteracReceivingRoute>().SingleOrDefaultAsync(x => x.Id == id && x.Environment == connection.EnvironmentName &&
            x.ConnectionKey == connection.ConnectionKey && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Receiving route not found for the current provider connection.");
    private static void CheckRevision(InteracReceivingRoute row, Guid revision)
    { if (row.Revision != revision) throw new InvalidOperationException("This receiving route changed. Refresh before saving."); }
    private InteracReceivingRouteDto ToDto(InteracReceivingRoute x) => new(x.Id, x.Revision, x.ProviderWalletConfigurationId,
        x.ProviderWalletId, x.ProviderRegistrationId, x.ReceivingEmail, x.DisplayName, x.Status, x.ProviderStatus,
        x.VerifiedAt, x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey && x.VerifiedAt.HasValue,
        x.FailureReason, x.IndividualEnabled, x.BusinessEnabled);
    private static InteracDepositDto DepositDto(InteracDeposit x) => new(x.Id, x.ProviderTransactionId, x.ProviderReference,
        x.Status, x.ReviewReason, x.SenderEmail, x.ReceivingEmail, x.CurrencyCode, x.GrossAmount, x.NetAmount, x.FeeAmount,
        x.FinancialAccountId, x.CollectionId, x.ReceivedAt, x.CreditedAt);
}
