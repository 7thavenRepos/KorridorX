using System.Data;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Audit;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.WalletWithdrawals;

// W2B2 consolidated lifecycle: single irreversible provider attempt per
// withdrawal, followed by GET-by-merchant-reference reconciliation. No POST
// retry is ever performed after a durable claim, including after timeouts,
// crashes, 422 responses or unknown network errors.
public sealed class WalletWithdrawalExecutionService : IWalletWithdrawalExecutionService
{
    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;
    private readonly IWalletWithdrawalQuoteGateway _quotes;
    private readonly IProviderPayoutLiquidityService _liquidity;
    private readonly IComplianceGateService _compliance;
    private readonly IOutboundFundsRestrictionService _restrictions;
    private readonly IProviderWalletResolver _wallets;
    private readonly PaymentChannelService _channels;
    private readonly IFinancialReservationService _reservations;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly IDataProtector _accounts;
    private readonly WalletWithdrawalProcessingOptions _settings;
    private readonly BlaaizOptions _blaaizSettings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WalletWithdrawalExecutionService> _logger;

    public WalletWithdrawalExecutionService(AppDbContext db, IRemittanceProvider provider,
        IWalletWithdrawalQuoteGateway quotes, IProviderPayoutLiquidityService liquidity,
        IComplianceGateService compliance,
        IOutboundFundsRestrictionService restrictions,
        IProviderWalletResolver wallets, PaymentChannelService channels,
        IFinancialReservationService reservations, INotificationQueueService notifications,
        IAuditService audit, IDataProtectionProvider dataProtection,
        IOptions<WalletWithdrawalProcessingOptions> settings, IOptions<BlaaizOptions> blaaizSettings,
        IHostEnvironment environment, ILogger<WalletWithdrawalExecutionService> logger)
    {
        _db = db; _provider = provider; _quotes = quotes; _liquidity = liquidity;
        _compliance = compliance;
        _restrictions = restrictions; _wallets = wallets;
        _channels = channels; _reservations = reservations; _notifications = notifications;
        _audit = audit; _settings = settings.Value; _blaaizSettings = blaaizSettings.Value;
        _environment = environment; _logger = logger;
        _accounts = dataProtection.CreateProtector("KorridorX.FiatWalletWithdrawalDestination.AccountNumber.v1");
    }

    private bool DispatchAllowed => WalletWithdrawalDispatchSafetyPolicy.CanDispatch(
        _environment.EnvironmentName, _settings, _blaaizSettings);

    private bool RecoverAllowed => WalletWithdrawalDispatchSafetyPolicy.CanRecover(
        _environment.EnvironmentName, _settings, _blaaizSettings);

    public async Task<int> DispatchBatchAsync(int batchSize, CancellationToken ct = default)
    {
        if (!DispatchAllowed) return 0;
        var ids = await _db.FiatWalletWithdrawals.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == FiatWalletWithdrawalStatus.AwaitingExecution &&
                x.PayoutId == null && x.OperationalHoldReason == null)
            .OrderBy(x => x.CreatedAt).Select(x => x.Id)
            .Take(Math.Clamp(batchSize, 1, 50)).ToListAsync(ct);
        var claimed = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await DispatchOneAsync(id, ct)) claimed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Never log destination account numbers or provider payloads.
                _logger.LogWarning("Withdrawal {WithdrawalId} was not dispatched: {ErrorType}",
                    id, ex.GetType().Name);
            }
        }
        return claimed;
    }

    public async Task<int> RecoverBatchAsync(int batchSize, CancellationToken ct = default)
    {
        if (!RecoverAllowed) return 0;
        var limit = Math.Clamp(batchSize, 1, 50);
        var cutoff = DateTime.UtcNow.AddSeconds(-15);
        // Scan beyond the oldest held entries so one deferred withdrawal
        // cannot starve newer provider transactions awaiting settlement.
        var candidates = await _db.FiatWalletWithdrawals.AsNoTracking()
            .Where(x => !x.IsDeleted && x.PayoutId != null &&
                (x.Status == FiatWalletWithdrawalStatus.Processing ||
                 x.Status == FiatWalletWithdrawalStatus.RequiresReconciliation) &&
                x.SubmittedToProviderAtUtc <= cutoff &&
                (x.LastUpdatedAt == null || x.LastUpdatedAt <= cutoff))
            .OrderBy(x => x.SubmittedToProviderAtUtc)
            .Select(x => new { x.Id, x.PayoutId })
            .Take(Math.Min(limit * 10, 500)).ToListAsync(ct);

        var attempted = 0;
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (attempted >= limit) break;
            try
            {
                // HTTP 422 with the exact Blaaiz note-length validation is
                // a known rejected submission. Pause *automatic* GET polling,
                // but preserve the claim, reservation and manual GET path.
                // After a strictly guarded admin retry, the original 422
                // remains audit history. Only the MOST RECENT POST can pause
                // automatic GET reconciliation for a known note rejection.
                var latestPost = await _db.ProviderRequestLogs.AsNoTracking()
                    .Where(x => x.RelatedPayoutId == candidate.PayoutId && x.HttpMethod == "POST")
                    .OrderByDescending(x => x.RequestedAt)
                    .Select(x => new { x.RequestedAt, x.ResponseStatusCode, x.ResponseBodyJson })
                    .FirstOrDefaultAsync(ct);
                if (latestPost is not null &&
                    WalletWithdrawalRetryPolicy.IsLatestKnownNoteRejection(
                        latestPost.ResponseStatusCode, latestPost.ResponseBodyJson))
                    continue;
                // A fresh POST starts a new recovery epoch. The original
                // 1120 GET 404s must not inflate cooldown of the retry.
                var currentAttemptStartUtc = latestPost?.RequestedAt ?? DateTime.UnixEpoch;

                // Other unknown outcomes continue reconciling, but consecutive
                // 404s receive an increasing cooldown capped at one hour.
                var recentGets = await _db.ProviderRequestLogs.AsNoTracking()
                    .Where(x => x.RelatedPayoutId == candidate.PayoutId && x.HttpMethod == "GET" &&
                        x.RequestedAt >= currentAttemptStartUtc)
                    .OrderByDescending(x => x.RequestedAt)
                    .Select(x => new { x.RequestedAt, x.ResponseStatusCode })
                    .Take(8).ToListAsync(ct);
                var consecutive404 = recentGets.TakeWhile(x => x.ResponseStatusCode == 404).Count();
                if (consecutive404 > 0 &&
                    WalletWithdrawalRecoveryPolicy.ShouldDeferAutomaticLookup(
                        DateTime.UtcNow, recentGets[0].RequestedAt, consecutive404))
                    continue;

                if (await RecoverOneAsync(candidate.Id, ct)) attempted++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("Withdrawal {WithdrawalId} reconciliation deferred: {ErrorType}",
                    candidate.Id, ex.GetType().Name);
            }
        }
        return attempted;
    }

    private async Task<bool> DispatchOneAsync(Guid id, CancellationToken ct)
    {
        if (!DispatchAllowed || !WalletWithdrawalDispatchSafetyPolicy.IsWithinConfiguredScope(
                id, _environment.EnvironmentName, _settings)) return false;
        var row = await _db.FiatWalletWithdrawals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id && !x.IsDeleted && x.Status == FiatWalletWithdrawalStatus.AwaitingExecution &&
            x.PayoutId == null && x.OperationalHoldReason == null, ct);
        if (row is null) return false;
        if (!WalletWithdrawalDispatchSafetyPolicy.IsWithinExecutionDebitLimit(
                row, _environment.EnvironmentName, _settings)) return false;
        WalletWithdrawalDispatchSafetyPolicy.ValidateSnapshot(row);
        var business = row.BusinessProfileId.HasValue;

        await _restrictions.EnsureFinancialAccountOwnerOutboundAllowedAsync(
            row.OwnerType, row.OwnerId, "FiatWalletWithdrawalDispatch", ct);
        if (business) await EnsureBusinessAuthorizationAsync(row, ct);

        var customer = business
            ? await _compliance.EnsureBusinessCanInitiateMoneyMovementAsync(row.BusinessProfileId!.Value,
                _provider.ProviderCode, ct)
            : await _compliance.EnsureCanInitiateMoneyMovementAsync(row.CustomerProfileId ??
                throw new InvalidOperationException("Withdrawal customer profile is missing."),
                _provider.ProviderCode, ct);
        if (string.IsNullOrWhiteSpace(customer.ProviderCustomerId))
            throw new InvalidOperationException("A verified provider customer ID is required.");

        // Decode/validate bank routing data BEFORE the irreversible provider claim.
        // Foreign details come from the encrypted, immutable withdrawal snapshot.
        var accountDetails = row.PaymentMethod != PaymentMethod.Interac
            ? _accounts.Unprotect(row.ProtectedAccountNumber ??
                throw new InvalidOperationException("Missing saved payout bank details.")) : null;
        var foreign = WalletWithdrawalForeignAccount.Supports(row.CurrencyCode, row.PaymentMethod)
            ? WalletWithdrawalForeignAccount.Parse(accountDetails, row.CurrencyCode, row.PaymentMethod) : null;
        var destinationCountry = WalletWithdrawalSetupPolicy.DestinationCountry(
            row.CurrencyCode, row.PaymentMethod, foreign?.Country);
        var eligible = await _channels.EligibleAsync(destinationCountry, row.CurrencyCode,
            business, PaymentChannelDirection.Withdrawal, row.CurrencyCode, row.TotalWalletDebit, ct);
        var choice = eligible.SingleOrDefault(x => x.Channel.Id == row.PaymentChannelId &&
            x.Capability.PaymentMethod == row.PaymentMethod)
            ?? throw new InvalidOperationException("The selected payout channel is no longer enabled.");
        if (!string.Equals(choice.Channel.ProviderCode, _provider.ProviderName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The withdrawal channel provider does not match the active remittance adapter.");

        // Re-price the original inclusive amount immediately before any
        // provider submission. A changed fee never silently reduces the net
        // amount the customer authorized.
        var fee = await _quotes.QuoteAsync(choice.Channel.ProviderCode, row.CurrencyCode,
            customer.ProviderCustomerId, row.TotalWalletDebit,
            row.CurrencyDecimalPlaces, row.PaymentMethod, ct);
        if (fee.ProviderFee != row.ProviderFee || fee.RecipientAmount != row.ExpectedRecipientAmount)
            throw new InvalidOperationException("The payout price changed. A new withdrawal quotation is required; funds remain held.");

        // Confirm actual provider liquidity before any irreversible claim or POST.
        // A failed preflight leaves the withdrawal safely awaiting execution.
        await _liquidity.EnsureAvailableAsync(choice.Channel.ProviderCode,
            row.CurrencyCode, row.TotalWalletDebit, choice.Wallet.ProviderWalletId, ct);

        // Commit the unique payout, route and one-shot claim BEFORE POST.
        // A crash from this point can only reconcile; no worker can POST again.
        var payoutId = await ClaimOnceAsync(row, choice, ct);
        if (!payoutId.HasValue) return false;

        try
        {
            var walletId = await _wallets.SelectAsync("Payout", payoutId.Value,
                _provider.ProviderName, row.CurrencyCode, null,
                ProviderWalletResolver.Payout, previouslySubmitted: false, ct);
            var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(row, payoutId.Value,
                customer.ProviderCustomerId, walletId, accountDetails);
            var result = await _provider.InitiatePayoutAsync(request, ct);
            await SaveAcknowledgementAsync(row.Id, payoutId.Value, result, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkUncertainAsync(row.Id, "Interrupted while awaiting payout confirmation", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await MarkUncertainAsync(row.Id, "Payout outcome unknown; verify provider merchant reference", CancellationToken.None);
            _logger.LogWarning("Withdrawal {WithdrawalId} requires provider lookup after submission attempt: {ErrorType}",
                row.Id, ex.GetType().Name);
            return true;
        }

        // Submission acceptance (even a SUCCESSFUL POST response) is never a
        // settlement event. Only the independent transaction GET may settle.
        try { await RecoverOneAsync(row.Id, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await MarkUncertainAsync(row.Id, "Provider settlement unverified; reconciliation required", CancellationToken.None);
            _logger.LogWarning("Withdrawal {WithdrawalId} awaits verified settlement: {ErrorType}",
                row.Id, ex.GetType().Name);
        }
        return true;
    }

    // Restricted to staging, SuperAdmin/Operations and a single provably
    // rejected note-validation POST. THIS METHOD SENDS MONEY ONLY WHEN THE
    // SEPARATELY DEPLOYED ADMIN ENDPOINT IS EXPLICITLY INVOKED.
    public async Task RetryKnownRejectedOnceAsync(Guid withdrawalId, Guid adminUserId,
        string expectedReference, string reason, CancellationToken ct = default)
    {
        if (_environment.EnvironmentName != Environments.Staging || !DispatchAllowed)
            throw new InvalidOperationException("One-time validated retry is available only in enabled staging.");
        if (adminUserId == Guid.Empty || string.IsNullOrWhiteSpace(expectedReference) ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new InvalidOperationException("An exact merchant reference and reason are required.");
        var auditReason = reason.Trim();
        var row = await _db.FiatWalletWithdrawals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == withdrawalId && !x.IsDeleted &&
            x.Status == FiatWalletWithdrawalStatus.RequiresReconciliation &&
            x.PayoutId != null, ct)
            ?? throw new InvalidOperationException("Withdrawal is not awaiting a claimed-payout reconciliation.");
        if (!string.Equals(expectedReference, row.Reference, StringComparison.Ordinal) ||
            !WalletWithdrawalDispatchSafetyPolicy.IsWithinExecutionDebitLimit(
                row, _environment.EnvironmentName, _settings))
            throw new InvalidOperationException("Reviewed withdrawal identity or amount is not eligible.");
        WalletWithdrawalDispatchSafetyPolicy.ValidateSnapshot(row);
        var business = row.BusinessProfileId.HasValue;

        // Perform the same current eligibility, compliance, route, fee and
        // liquidity checks as normal dispatch, BEFORE any durable retry claim.
        await _restrictions.EnsureFinancialAccountOwnerOutboundAllowedAsync(
            row.OwnerType, row.OwnerId, "FiatWalletWithdrawalAdminRetry", ct);
        if (business) await EnsureBusinessAuthorizationAsync(row, ct);
        var customer = business
            ? await _compliance.EnsureBusinessCanInitiateMoneyMovementAsync(
                row.BusinessProfileId!.Value, _provider.ProviderCode, ct)
            : await _compliance.EnsureCanInitiateMoneyMovementAsync(
                row.CustomerProfileId ?? throw new InvalidOperationException(
                    "Withdrawal customer profile is missing."), _provider.ProviderCode, ct);
        if (string.IsNullOrWhiteSpace(customer.ProviderCustomerId))
            throw new InvalidOperationException("A verified payout-provider customer is required.");

        var accountDetails = row.PaymentMethod != PaymentMethod.Interac
            ? _accounts.Unprotect(row.ProtectedAccountNumber ??
                throw new InvalidOperationException("Original protected bank account is missing."))
            : null;
        var foreign = WalletWithdrawalForeignAccount.Supports(row.CurrencyCode, row.PaymentMethod)
            ? WalletWithdrawalForeignAccount.Parse(accountDetails, row.CurrencyCode, row.PaymentMethod)
            : null;
        var destinationCountry = WalletWithdrawalSetupPolicy.DestinationCountry(
            row.CurrencyCode, row.PaymentMethod, foreign?.Country);
        var eligible = await _channels.EligibleAsync(destinationCountry, row.CurrencyCode,
            business, PaymentChannelDirection.Withdrawal, row.CurrencyCode, row.TotalWalletDebit, ct);
        var choice = eligible.SingleOrDefault(x => x.Channel.Id == row.PaymentChannelId &&
            x.Capability.PaymentMethod == row.PaymentMethod)
            ?? throw new InvalidOperationException("The original withdrawal payout channel is disabled.");
        if (!string.Equals(choice.Channel.ProviderCode, _provider.ProviderName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider changed since the original withdrawal.");

        var fee = await _quotes.QuoteAsync(choice.Channel.ProviderCode, row.CurrencyCode,
            customer.ProviderCustomerId, row.TotalWalletDebit,
            row.CurrencyDecimalPlaces, row.PaymentMethod, ct);
        if (fee.ProviderFee != row.ProviderFee || fee.RecipientAmount != row.ExpectedRecipientAmount)
            throw new InvalidOperationException(
                "Payout pricing changed. Admin retry refused; reserved funds remain held.");
        var payoutId = row.PayoutId!.Value;
        var walletId = await _wallets.SelectAsync("Payout", payoutId,
            _provider.ProviderName, row.CurrencyCode, null,
            ProviderWalletResolver.Payout, previouslySubmitted: true, ct);

        // Only the saved provider wallet may fund this retry. Never
        // check liquidity on a newly selected channel wallet.
        await _liquidity.EnsureAvailableAsync(choice.Channel.ProviderCode, row.CurrencyCode,
            row.TotalWalletDebit, walletId, ct);
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(
            row, payoutId, customer.ProviderCustomerId, walletId, accountDetails);

        // This transaction locks and revalidates *all* sensitive evidence,
        // preserves the old failed attempt, and claims the only allowed retry.
        // No provider HTTP call occurs before its commit.
        await ClaimKnownRejectedRetryOnceAsync(row, adminUserId,
            expectedReference, auditReason, ct);

        try
        {
            var result = await _provider.InitiatePayoutAsync(request, ct);
            await SaveAcknowledgementAsync(row.Id, payoutId, result, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkUncertainAsync(row.Id, "Admin retry interrupted; provider verification required",
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await MarkUncertainAsync(row.Id,
                "Admin retry outcome unknown; verify the original merchant reference",
                CancellationToken.None);
            _logger.LogWarning(
                "Admin-authorized one-shot payout retry {WithdrawalId} needs reconciliation: {ErrorType}",
                row.Id, ex.GetType().Name);
            return;
        }

        // POST acknowledgement is NEVER financial settlement.
        // Only independently verified provider GET may capture or release.
        try { await RecoverOneAsync(row.Id, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await MarkUncertainAsync(row.Id,
                "Admin retry accepted, independent provider settlement unverified",
                CancellationToken.None);
            _logger.LogWarning(
                "Admin-authorized retry {WithdrawalId} still awaits GET: {ErrorType}",
                row.Id, ex.GetType().Name);
        }
    }

    private async Task ClaimKnownRejectedRetryOnceAsync(FiatWalletWithdrawal snapshot,
        Guid adminUserId, string expectedReference, string reason, CancellationToken ct)
    {
        // Preflight is read-only. Reject any pending writes in this scoped DbContext
        // so the retry claim cannot accidentally save an unrelated, stale entity.
        if (_db.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "The retry preflight left unsaved database changes. No payout was submitted.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {snapshot.Id} FOR UPDATE")
                .AsNoTracking()
                .SingleAsync(ct);
            if (row.PayoutId is not Guid payoutId ||
                row.PayoutId != snapshot.PayoutId ||
                !string.Equals(row.Reference, expectedReference, StringComparison.Ordinal) ||
                row.RequestFingerprint != snapshot.RequestFingerprint ||
                row.TotalWalletDebit != snapshot.TotalWalletDebit ||
                row.ProviderFee != snapshot.ProviderFee ||
                row.ExpectedRecipientAmount != snapshot.ExpectedRecipientAmount ||
                row.FinancialReservationId != snapshot.FinancialReservationId ||
                row.PaymentChannelId != snapshot.PaymentChannelId)
                throw new InvalidOperationException(
                    "Withdrawal changed since the preflight. No provider POST was attempted.");

            var payout = await _db.Payouts.AsNoTracking().Include(x => x.Attempts)
                .SingleAsync(x => x.Id == payoutId, ct);
            var logs = _db.ProviderRequestLogs.AsNoTracking()
                .Where(x => x.RelatedPayoutId == payoutId);
            var firstPost = await logs.Where(x => x.HttpMethod == "POST")
                .OrderBy(x => x.RequestedAt).FirstOrDefaultAsync(ct);
            var posts = await logs.CountAsync(x => x.HttpMethod == "POST", ct);
            var gets404 = await logs.CountAsync(x =>
                x.HttpMethod == "GET" && x.ResponseStatusCode == 404, ct);
            var otherGets = await logs.CountAsync(x =>
                x.HttpMethod == "GET" && x.ResponseStatusCode != 404, ct);
            var last404Utc = await logs.Where(x =>
                x.HttpMethod == "GET" && x.ResponseStatusCode == 404)
                .MaxAsync(x => (DateTime?)x.RequestedAt, ct);
            if (!WalletWithdrawalRetryPolicy.CanRetryOnce(
                    row, payout, firstPost, posts, gets404, otherGets,
                    last404Utc, DateTime.UtcNow))
                throw new InvalidOperationException(
                    "Safe one-shot retry proof failed. Existing hold and provider claim are preserved.");

            if (row.BusinessProfileId.HasValue)
                await EnsureBusinessAuthorizationAsync(row, ct);
            _ = await _db.FinancialAccounts.FromSqlInterpolated(
                    $"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {row.FinancialAccountId} FOR UPDATE")
                .AsNoTracking()
                .SingleAsync(ct);
            var reservation = await _db.FinancialReservations.AsNoTracking().SingleAsync(x =>
                x.Id == row.FinancialReservationId &&
                x.FinancialAccountId == row.FinancialAccountId &&
                x.RelatedEntityId == row.Id &&
                x.RelatedEntityType == nameof(FiatWalletWithdrawal), ct);
            if (reservation.IsDeleted ||
                reservation.Status != FinancialReservationStatus.Active ||
                reservation.Type != FinancialReservationType.Withdrawal ||
                reservation.Amount != row.TotalWalletDebit ||
                reservation.CapturedAmount != 0m || reservation.ReleasedAmount != 0m)
                throw new InvalidOperationException("Original wallet reservation is not fully held.");

            var originalAttempt = payout.Attempts.Single();
            var now = DateTime.UtcNow;
            var originalRequestId = firstPost!.Id.ToString("D");

            // Execute guarded updates directly in the same transaction instead of
            // asking SaveChanges to flush several tracked concurrency-token entities.
            // Each expected row must still exist in its original state; a zero-row
            // update is a hard stop, never permission to submit a payout.
            var changedAttempt = await _db.PayoutAttempts.Where(x =>
                    x.Id == originalAttempt.Id && x.PayoutId == payoutId &&
                    x.Status == ProviderRequestStatus.Pending &&
                    x.ProviderRequestId == null && x.ProviderResponseId == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Status, ProviderRequestStatus.Failed)
                    .SetProperty(x => x.ProviderRequestId, originalRequestId)
                    .SetProperty(x => x.ErrorMessage,
                        WalletWithdrawalRecoveryPolicy.BlaaizNoteLimitMessage), ct);
            if (changedAttempt != 1)
                throw new InvalidOperationException(
                    "The original payout attempt changed. No retry was claimed.");

            var changedPayout = await _db.Payouts.Where(x =>
                    x.Id == payoutId && x.Status == PayoutStatus.Processing &&
                    x.ProviderPayoutId == null &&
                    (x.ProviderReference == null || x.ProviderReference == ""))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.LastUpdatedAt, now), ct);
            if (changedPayout != 1)
                throw new InvalidOperationException(
                    "The payout changed. No retry was claimed.");

            var changedWithdrawal = await _db.FiatWalletWithdrawals.Where(x =>
                    x.Id == row.Id &&
                    x.Status == FiatWalletWithdrawalStatus.RequiresReconciliation &&
                    x.PayoutId == payoutId &&
                    x.Reference == expectedReference &&
                    x.RequestFingerprint == snapshot.RequestFingerprint &&
                    x.TotalWalletDebit == snapshot.TotalWalletDebit &&
                    x.ProviderFee == snapshot.ProviderFee &&
                    x.ExpectedRecipientAmount == snapshot.ExpectedRecipientAmount &&
                    x.FinancialReservationId == snapshot.FinancialReservationId &&
                    x.PaymentChannelId == snapshot.PaymentChannelId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Status, FiatWalletWithdrawalStatus.Processing)
                    .SetProperty(x => x.OperationalHoldReason, (string?)null)
                    .SetProperty(x => x.SubmittedToProviderAtUtc, now)
                    .SetProperty(x => x.LastUpdatedAt, now)
                    .SetProperty(x => x.LastUpdatedByUserId, adminUserId), ct);
            if (changedWithdrawal != 1)
                throw new InvalidOperationException(
                    "The withdrawal changed. No retry was claimed.");

            // Only the new attempt and audit record are inserted by SaveChanges.
            // No existing financial account or reservation is mutated here.
            _db.PayoutAttempts.Add(new PayoutAttempt
            {
                PayoutId = payout.Id, Status = ProviderRequestStatus.Pending, AttemptedAt = now,
                RequestPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    withdrawal = row.Reference, retry = "admin_known_note_validation",
                    row.TotalWalletDebit, row.ExpectedRecipientAmount, row.ProviderFee,
                    row.CurrencyCode, method = row.PaymentMethod.ToString(),
                    merchantReference = row.Reference
                })
            });
            _audit.Stage(new AuditRecordRequest(
                "FIAT_WALLET_WITHDRAWAL_ADMIN_KNOWN_422_RETRY_CLAIMED",
                "WalletWithdrawal", nameof(FiatWalletWithdrawal), row.Id.ToString(),
                NewValues: new
                {
                    row.Reference, payout.Id, PreviousProviderRequestId = firstPost.Id,
                    TotalAttempts = payout.Attempts.Count + 1,
                    row.TotalWalletDebit, row.ExpectedRecipientAmount
                },
                Metadata: new { Reason = reason, OriginalHttpStatus = 422 },
                UserId: adminUserId));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Deliberately log entity TYPES only: no bank details, provider
            // credentials, or request payloads can enter this diagnostic log.
            _logger.LogError(ex,
                "One-time payout claim hit EF concurrency on entities: {EntityTypes}",
                string.Join(",", ex.Entries.Select(x => x.Metadata.ClrType.Name).Distinct()));
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Guid?> ClaimOnceAsync(FiatWalletWithdrawal snapshot, PaymentChannelChoice choice,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {snapshot.Id} FOR UPDATE")
                .SingleAsync(ct);
            if (!WalletWithdrawalDispatchSafetyPolicy.CanClaim(row))
            {
                await tx.CommitAsync(ct);
                return null;
            }
            if (row.RequestFingerprint != snapshot.RequestFingerprint ||
                row.TotalWalletDebit != snapshot.TotalWalletDebit ||
                row.ExpectedRecipientAmount != snapshot.ExpectedRecipientAmount ||
                row.PaymentChannelId != snapshot.PaymentChannelId ||
                row.FinancialReservationId != snapshot.FinancialReservationId)
                throw new InvalidOperationException("Withdrawal data changed while preparing provider dispatch.");
            var held = await _db.FinancialReservations.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == row.FinancialReservationId && x.FinancialAccountId == row.FinancialAccountId &&
                x.RelatedEntityId == row.Id && x.RelatedEntityType == nameof(FiatWalletWithdrawal) &&
                x.Status == FinancialReservationStatus.Active &&
                x.Type == FinancialReservationType.Withdrawal && !x.IsDeleted, ct);
            if (held is null || held.Amount != row.TotalWalletDebit ||
                held.CapturedAmount != 0m || held.ReleasedAmount != 0m)
                throw new InvalidOperationException("The held wallet reservation is not valid for provider dispatch.");

            // The operations worker does not have an HTTP-selected business
            // context. Recheck persisted membership and approval constraints
            // at the durable provider-claim boundary as well as preflight.
            if (row.BusinessProfileId.HasValue)
                await EnsureBusinessAuthorizationAsync(row, ct);

            var payout = new Payout
            {
                FinancialAccountId = row.FinancialAccountId,
                Purpose = PaymentOperationPurpose.Withdrawal,
                RelatedEntityType = nameof(FiatWalletWithdrawal), RelatedEntityId = row.Id,
                Reference = row.Reference, CurrencyCode = row.CurrencyCode,
                Amount = row.ExpectedRecipientAmount,
                PaymentChannelId = row.PaymentChannelId, PaymentChannelName = choice.Channel.DisplayName,
                PaymentMethod = row.PaymentMethod, ProviderCode = _provider.ProviderName,
                Status = PayoutStatus.Processing, InitiatedAt = DateTime.UtcNow,
                CreatedByUserId = row.RequestedByUserId
            };
            payout.Attempts.Add(new PayoutAttempt
            {
                Status = ProviderRequestStatus.Pending, AttemptedAt = DateTime.UtcNow,
                RequestPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    withdrawal = row.Reference, mode = "from_amount", row.TotalWalletDebit,
                    row.ExpectedRecipientAmount, row.ProviderFee, row.CurrencyCode,
                    method = row.PaymentMethod.ToString(), merchantReference = row.Reference
                })
            });
            _db.Payouts.Add(payout);
            _channels.Record(choice, "Payout", payout.Id, choice.Channel.CountryCode == "*"
                ? WalletWithdrawalSetupPolicy.DestinationCountry(row.CurrencyCode, row.PaymentMethod)
                : choice.Channel.CountryCode, row.BusinessProfileId.HasValue, row.OwnerId,
                row.FinancialAccountId, row.TotalWalletDebit);
            row.PayoutId = payout.Id;
            row.Status = FiatWalletWithdrawalStatus.Processing;
            row.SubmittedToProviderAtUtc = DateTime.UtcNow; // durable possible-submission boundary
            row.LastUpdatedAt = DateTime.UtcNow;
            _audit.Stage(new AuditRecordRequest("FIAT_WALLET_WITHDRAWAL_DISPATCH_CLAIMED",
                "WalletWithdrawal", nameof(FiatWalletWithdrawal), row.Id.ToString(), NewValues: new
                { row.Reference, payout.Id, row.TotalWalletDebit, row.ExpectedRecipientAmount },
                UserId: row.RequestedByUserId));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return payout.Id;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task EnsureBusinessAuthorizationAsync(FiatWalletWithdrawal row, CancellationToken ct)
    {
        var profile = await _db.BusinessProfiles.AsNoTracking()
            .Where(x => x.Id == row.OwnerId && !x.IsDeleted)
            .Select(x => new { x.OwnerUserId, x.RequiresTransferApproval,
                x.TransferApprovalThreshold }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Withdrawal business profile is unavailable.");
        if (profile.RequiresTransferApproval && (!profile.TransferApprovalThreshold.HasValue ||
                row.TotalWalletDebit >= profile.TransferApprovalThreshold.Value))
            throw new InvalidOperationException("Business withdrawal requires a separate approval before execution.");
        if (profile.OwnerUserId == row.RequestedByUserId) return;
        var membership = await _db.BusinessUsers.AsNoTracking()
            .Where(x => x.BusinessProfileId == row.OwnerId &&
                x.UserId == row.RequestedByUserId && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Role, x.Permissions }).SingleOrDefaultAsync(ct);
        if (membership is null)
            throw new InvalidOperationException("Withdrawal initiator is no longer a business member.");
        var permissions = membership.Permissions == BusinessPermission.None
            ? BusinessAccessService.DefaultPermissions(membership.Role) : membership.Permissions;
        if ((permissions & BusinessPermission.ManageFunding) != BusinessPermission.ManageFunding)
            throw new InvalidOperationException("Withdrawal initiator no longer has funding permissions.");
    }

    private async Task SaveAcknowledgementAsync(Guid withdrawalId, Guid payoutId,
        RemittancePayoutResult result, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {withdrawalId} FOR UPDATE")
                .SingleAsync(ct);
            if (row.PayoutId != payoutId || row.Status is not (
                    FiatWalletWithdrawalStatus.Processing or FiatWalletWithdrawalStatus.RequiresReconciliation))
                throw new InvalidOperationException("The claimed payout is no longer in a recoverable state.");
            var payout = await _db.Payouts.Include(x => x.Attempts).SingleAsync(x => x.Id == payoutId, ct);
            if (!string.IsNullOrWhiteSpace(payout.ProviderPayoutId) &&
                payout.ProviderPayoutId != result.ProviderTransactionId)
                throw new InvalidOperationException("Provider payout identity changed; reconcile before settlement.");
            payout.ProviderPayoutId = result.ProviderTransactionId;
            payout.ProviderReference = result.ProviderReference;
            var attempt = payout.Attempts.OrderByDescending(x => x.AttemptedAt).First();
            attempt.ProviderRequestId = result.ProviderRequestLogId.ToString();
            attempt.ProviderResponseId = result.ProviderTransactionId;
            attempt.Status = ProviderRequestStatus.Successful;
            attempt.ResponsePayloadJson = result.RawResponseJson;
            payout.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> RecoverOneAsync(Guid withdrawalId, CancellationToken ct = default)
    {
        if ((!RecoverAllowed && !DispatchAllowed) ||
            !WalletWithdrawalDispatchSafetyPolicy.IsWithinConfiguredScope(
                withdrawalId, _environment.EnvironmentName, _settings)) return false;
        var row = await _db.FiatWalletWithdrawals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == withdrawalId && !x.IsDeleted && x.PayoutId != null &&
            (x.Status == FiatWalletWithdrawalStatus.Processing ||
             x.Status == FiatWalletWithdrawalStatus.RequiresReconciliation), ct);
        if (row is null) return false;
        var payout = await _db.Payouts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.PayoutId && !x.IsDeleted && x.Purpose == PaymentOperationPurpose.Withdrawal &&
            x.RelatedEntityId == row.Id && x.RelatedEntityType == nameof(FiatWalletWithdrawal), ct)
            ?? throw new InvalidOperationException("Withdrawal payout mapping is missing or invalid.");

        // Blaaiz resolves GET by provider ID, reference, or unique
        // merchant_reference. A missing POST response therefore does not
        // authorize a second POST, even after the service restarts.
        var key = string.IsNullOrWhiteSpace(payout.ProviderPayoutId) ? row.Reference : payout.ProviderPayoutId;
        var verified = await _provider.GetTransactionAsync(key, payoutId: payout.Id, ct: ct);
        await ApplyVerifiedAsync(row.Id, verified, ct);
        return true;
    }

    private async Task ApplyVerifiedAsync(Guid withdrawalId,
        RemittanceTransactionStatusResult verified, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {withdrawalId} FOR UPDATE")
                .SingleAsync(ct);
            if (row.Status is FiatWalletWithdrawalStatus.Succeeded or
                FiatWalletWithdrawalStatus.FailedReleased or FiatWalletWithdrawalStatus.CancelledReleased)
            {
                await tx.CommitAsync(ct);
                return;
            }
            if (row.PayoutId is not Guid payoutId || row.FinancialReservationId is null ||
                row.Status is not (FiatWalletWithdrawalStatus.Processing or
                    FiatWalletWithdrawalStatus.RequiresReconciliation))
                throw new InvalidOperationException("Withdrawal is not in a provider-verifiable state.");
            var payout = await _db.Payouts.Include(x => x.Attempts)
                .SingleAsync(x => x.Id == payoutId && x.Purpose == PaymentOperationPurpose.Withdrawal &&
                    x.RelatedEntityId == row.Id && x.RelatedEntityType == nameof(FiatWalletWithdrawal), ct);
            var decision = WalletWithdrawalSettlementPolicy.Classify(row, payout.ProviderPayoutId, verified);
            var firstHold = row.Status != FiatWalletWithdrawalStatus.RequiresReconciliation;
            if (decision == WalletWithdrawalSettlementDecision.RequireReconciliation)
            {
                row.Status = FiatWalletWithdrawalStatus.RequiresReconciliation;
                row.OperationalHoldReason = "Provider transaction evidence is missing or inconsistent. Manual review required.";
            }
            else if (decision == WalletWithdrawalSettlementDecision.AwaitProvider)
            {
                // Unknown earlier lookups may be due to replication lag;
                // a later consistent pending response clears only this
                // automated reconciliation hold, never a manually set hold.
                if (row.OperationalHoldReason is null || row.OperationalHoldReason.StartsWith(
                        "Provider transaction evidence", StringComparison.Ordinal))
                {
                    row.Status = FiatWalletWithdrawalStatus.Processing;
                    row.OperationalHoldReason = null;
                }
                payout.ProviderPayoutId ??= verified.ProviderTransactionId;
                payout.ProviderReference = verified.ProviderReference;
                payout.Status = PayoutStatus.Processing;
            }
            else
            {
                // All financial mutations (reservation + wallet + ledger,
                // withdrawal status and notifications) commit atomically.
                _ = await _db.FinancialAccounts.FromSqlInterpolated(
                        $"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {row.FinancialAccountId} FOR UPDATE")
                    .SingleAsync(ct);
                var reservation = await _db.FinancialReservations.AsNoTracking().SingleAsync(x =>
                    x.Id == row.FinancialReservationId && x.FinancialAccountId == row.FinancialAccountId &&
                    x.RelatedEntityType == nameof(FiatWalletWithdrawal) && x.RelatedEntityId == row.Id, ct);
                if (reservation.Status != FinancialReservationStatus.Active ||
                    reservation.Type != FinancialReservationType.Withdrawal ||
                    reservation.Amount != row.TotalWalletDebit || reservation.CapturedAmount != 0m ||
                    reservation.ReleasedAmount != 0m)
                    throw new InvalidOperationException("Withdrawal financial reservation is no longer wholly held.");
                if (decision == WalletWithdrawalSettlementDecision.Capture)
                {
                    await _reservations.CaptureAsync(reservation.Id, row.TotalWalletDebit, null, ct);
                    row.Status = FiatWalletWithdrawalStatus.Succeeded;
                    payout.Status = PayoutStatus.Successful;
                    payout.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    await _reservations.ReleaseAsync(reservation.Id, row.TotalWalletDebit,
                        "Verified Blaaiz withdrawal payout rejected", null, ct);
                    row.Status = FiatWalletWithdrawalStatus.FailedReleased;
                    payout.Status = PayoutStatus.Failed;
                    payout.FailedAt = DateTime.UtcNow;
                    payout.FailureReason = "Provider confirmed payout failed; wallet funds released.";
                }
                row.SettledAtUtc = DateTime.UtcNow;
                row.OperationalHoldReason = null;
                payout.ProviderPayoutId ??= verified.ProviderTransactionId;
                payout.ProviderReference = verified.ProviderReference;
                var succeeded = decision == WalletWithdrawalSettlementDecision.Capture;
                var subject = succeeded ? "Wallet withdrawal completed" : "Wallet withdrawal unsuccessful";
                var message = succeeded
                    ? "<p>Your withdrawal was confirmed by the payout provider.</p>"
                    : "<p>The payout provider confirmed that your withdrawal failed. The reserved funds have been returned to your available wallet balance.</p>";
                if (row.BusinessProfileId is Guid businessId)
                    await _notifications.QueueBusinessAsync(businessId, subject, message,
                        nameof(FiatWalletWithdrawal), row.Id, ct);
                else
                    await _notifications.QueueUserAsync(row.RequestedByUserId, subject, message,
                        nameof(FiatWalletWithdrawal), row.Id, ct);
                _audit.Stage(new AuditRecordRequest(succeeded
                    ? "FIAT_WALLET_WITHDRAWAL_VERIFIED_CAPTURE"
                    : "FIAT_WALLET_WITHDRAWAL_VERIFIED_RELEASE",
                    "WalletWithdrawal", nameof(FiatWalletWithdrawal), row.Id.ToString(),
                    NewValues: new { row.Reference, row.Status, row.TotalWalletDebit,
                        verified.ProviderTransactionId, verified.ProviderStatus }, UserId: null));
            }

            if (decision == WalletWithdrawalSettlementDecision.RequireReconciliation && firstHold)
            {
                const string subject = "Wallet withdrawal under review";
                const string body = "<p>Your withdrawal is awaiting verification with the payout provider. Funds remain reserved and will not be deducted again.</p>";
                if (row.BusinessProfileId is Guid businessId)
                    await _notifications.QueueBusinessAsync(businessId, subject, body,
                        nameof(FiatWalletWithdrawal), row.Id, ct);
                else
                    await _notifications.QueueUserAsync(row.RequestedByUserId, subject, body,
                        nameof(FiatWalletWithdrawal), row.Id, ct);
            }
            row.LastUpdatedAt = DateTime.UtcNow;
            payout.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task MarkUncertainAsync(Guid withdrawalId, string genericReason, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {withdrawalId} FOR UPDATE")
                .SingleAsync(ct);
            if (row.Status is FiatWalletWithdrawalStatus.Processing or
                FiatWalletWithdrawalStatus.RequiresReconciliation)
            {
                if (row.Status != FiatWalletWithdrawalStatus.RequiresReconciliation)
                {
                    row.Status = FiatWalletWithdrawalStatus.RequiresReconciliation;
                    row.OperationalHoldReason = genericReason;
                    const string subject = "Wallet withdrawal under review";
                    const string message = "<p>Your payout is being verified. Your wallet reservation remains protected while we investigate.</p>";
                    if (row.BusinessProfileId is Guid businessId)
                        await _notifications.QueueBusinessAsync(businessId, subject, message,
                            nameof(FiatWalletWithdrawal), row.Id, ct);
                    else
                        await _notifications.QueueUserAsync(row.RequestedByUserId, subject, message,
                            nameof(FiatWalletWithdrawal), row.Id, ct);
                }
                row.LastUpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task CancelUnsubmittedAsync(Guid withdrawalId, Guid adminUserId, string reason,
        CancellationToken ct = default)
    {
        if (!WalletWithdrawalDispatchSafetyPolicy.CanInitiate(_environment.EnvironmentName, _settings))
            throw new InvalidOperationException("Wallet withdrawal cancellation is disabled in this environment.");
        if (adminUserId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new InvalidOperationException("A valid admin and cancellation reason are required.");

        // Lock the withdrawal first. A previously claimed payout can never be
        // cancelled/released on assumption of failure: it must be reconciled.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var row = await _db.FiatWalletWithdrawals.FromSqlInterpolated(
                    $"SELECT * FROM \"FiatWalletWithdrawals\" WHERE \"Id\" = {withdrawalId} FOR UPDATE")
                .SingleAsync(ct);
            if (!WalletWithdrawalDispatchSafetyPolicy.CanCancelUnsubmitted(row) ||
                row.FinancialReservationId is null)
                throw new InvalidOperationException("Only a never-submitted wallet withdrawal can be cancelled safely.");
            _ = await _db.FinancialAccounts.FromSqlInterpolated(
                    $"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {row.FinancialAccountId} FOR UPDATE")
                .SingleAsync(ct);
            var reservation = await _db.FinancialReservations.AsNoTracking().SingleAsync(x =>
                x.Id == row.FinancialReservationId && x.FinancialAccountId == row.FinancialAccountId &&
                x.RelatedEntityType == nameof(FiatWalletWithdrawal) && x.RelatedEntityId == row.Id, ct);
            if (reservation.Status != FinancialReservationStatus.Active ||
                reservation.Type != FinancialReservationType.Withdrawal ||
                reservation.Amount != row.TotalWalletDebit || reservation.CapturedAmount != 0m ||
                reservation.ReleasedAmount != 0m)
                throw new InvalidOperationException("The withdrawal reservation is no longer wholly held.");
            await _reservations.ReleaseAsync(reservation.Id, row.TotalWalletDebit,
                "Admin cancelled never-submitted withdrawal: " + reason.Trim(), adminUserId, ct);
            row.Status = FiatWalletWithdrawalStatus.CancelledReleased;
            row.SettledAtUtc = DateTime.UtcNow;
            row.OperationalHoldReason = null;
            row.LastUpdatedAt = DateTime.UtcNow;
            row.LastUpdatedByUserId = adminUserId;
            const string subject = "Wallet withdrawal cancelled";
            const string body = "<p>Your withdrawal was cancelled before being sent to the payout provider. The reserved funds have been restored to your available wallet balance.</p>";
            if (row.BusinessProfileId is Guid businessId)
                await _notifications.QueueBusinessAsync(businessId, subject, body,
                    nameof(FiatWalletWithdrawal), row.Id, ct);
            else
                await _notifications.QueueUserAsync(row.RequestedByUserId, subject, body,
                    nameof(FiatWalletWithdrawal), row.Id, ct);
            _audit.Stage(new AuditRecordRequest("FIAT_WALLET_WITHDRAWAL_ADMIN_CANCELLED",
                "WalletWithdrawal", nameof(FiatWalletWithdrawal), row.Id.ToString(),
                NewValues: new { row.Reference, row.TotalWalletDebit, row.Status },
                Metadata: new { Reason = reason.Trim() }, UserId: adminUserId));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
