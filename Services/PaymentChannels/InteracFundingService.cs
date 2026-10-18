using System.Data;
using System.Text.Encodings.Web;
using KorridorX.Data;
using KorridorX.Configuration;
using Microsoft.Extensions.Options;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.Notifications;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

public sealed class InteracFundingService(AppDbContext db, WalletFundingContextService contexts,
    IProviderWalletResolver connection, INotificationQueueService notifications, IOptions<BlaaizOptions> options)
{
    public async Task<InteracFundingDto> GetAsync(Guid actor, Guid walletId, bool business, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(actor, walletId, business, false, false, ct);
        if (context.Wallet.AssetCode != "CAD") return Empty(walletId, context.Wallet.AssetCode, "Interac receiving is available only for CAD wallets.");
        var route = await CurrentRouteAsync(business, ct);
        if (route is null) return Empty(walletId, "CAD", "Interac receiving is not configured for this wallet. Use another available Add money method.");
        string? defaultEmail = null; string? defaultStatus = null;
        if (!business)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor, ct);
            defaultEmail = InteracSecurity.OptionalEmail(user.Email);
            defaultStatus = !user.EmailConfirmed ? "Unverified" : defaultEmail is null ? "Unavailable" : null;
            await RevokeStaleDefaultsAsync(actor, walletId, route.Id, user.EmailConfirmed ? defaultEmail : null, ct);
            if (defaultStatus is null && route.Status == "Active")
                defaultStatus = await EnsureDefaultAsync(actor, context.Wallet.Id, route.Id, defaultEmail!, ct);
        }
        var now = DateTime.UtcNow;
        var rows = await db.Set<InteracSendingEmail>().AsNoTracking().Where(x => x.RouteId == route.Id &&
            x.FinancialAccountId == walletId && x.Status != "Revoked" && !x.IsDeleted)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        var walletReady = context.Wallet.Status == FinancialAccountStatus.Active && context.Wallet.Asset.IsSupported &&
            context.Wallet.Asset.DepositEnabled && (business ? context.Wallet.Asset.BusinessEnabled : context.Wallet.Asset.IndividualEnabled) &&
            await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == context.CountryCode && x.AssetCode == "CAD" &&
                x.CanDeposit && x.Country.IsSupported, ct);
        var providerReady = options.Value.IsEnabled && route.Status == "Active" && route.VerifiedAt.HasValue && route.ConnectionKey == connection.ConnectionKey &&
            await db.ProviderWalletConfigurations.AsNoTracking().AnyAsync(x => x.Id == route.ProviderWalletConfigurationId &&
                x.ProviderWalletId == route.ProviderWalletId && x.Environment == connection.EnvironmentName && !x.IsDeleted &&
                x.IsActive && x.CollectionEnabled && x.VerifiedConnectionKey == connection.ConnectionKey && x.VerifiedAt != null, ct);
        var available = providerReady && walletReady && rows.Any(x => x.Status == "Verified");
        var message = available ? "Send CAD Interac from a verified sending email below. Confirmed deposits are added to this wallet automatically."
            : defaultStatus == "Conflict" ? "Your registration email is already assigned to another wallet on this receiving route. Verify a different sending email or contact support."
            : !providerReady ? "The receiving route is not active. Do not send funds until these instructions show a ready receiving email."
            : !walletReady ? "Deposits are not currently enabled for this wallet. Contact support before sending funds."
            : "Verify a sending email to reveal your persistent Interac receiving instructions. Your bank's Interac sender email must match it exactly.";
        return new(walletId, "CAD", available, available ? route.ReceivingEmail : null, route.Id, route.Status, message,
            InteracSecurity.MaximumSendingEmails, defaultEmail, defaultStatus,
            rows.Select(x => new InteracSendingEmailDto(x.Id, x.Email,
                x.Status == "Pending" && (x.CodeExpiresAt <= now || x.FailedAttempts >= InteracSecurity.MaximumCodeAttempts) ? "Expired" : x.Status,
                x.IsDefault, x.VerifiedAt, x.CodeExpiresAt, x.CanResendAt)).ToArray(),
            ["Send CAD only, using the exact verified sender email configured with your bank.",
             "Use the receiving email shown here; it stays available without creating a transfer or a Money Request.",
             "Your wallet receives the provider-confirmed net amount after any provider fee, once only.",
             "Do not send from an unverified or shared third-party fintech sender email. Unmatched deposits require support review.",
             "Keep the bank/provider transaction reference. A transfer is optional and is a separate action after funding."]);
    }

    public async Task<InteracFundingDto> AddAsync(Guid actor, Guid walletId, bool business, string email, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(actor, walletId, business, true, true, ct);
        if (context.Wallet.AssetCode != "CAD") throw new InvalidOperationException("Sending emails are available only for CAD wallets.");
        var route = await CurrentRouteAsync(business, ct);
        if (route?.Status != "Active") throw new InvalidOperationException("An active Interac receiving route is required.");
        var normalized = InteracSecurity.NormalizeEmail(email);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
            var row = await db.Set<InteracSendingEmail>().SingleOrDefaultAsync(x => x.RouteId == route.Id &&
                x.FinancialAccountId == walletId && x.NormalizedEmail == normalized, ct);
            if (row?.Status == "Verified") throw new InvalidOperationException("This sending email is already verified for the wallet.");
            if (row?.CanResendAt > DateTime.UtcNow) throw new InvalidOperationException("Wait until the resend time before requesting another code.");
            if (row is null || row.Status == "Revoked")
            {
                var count = await db.Set<InteracSendingEmail>().CountAsync(x => x.RouteId == route.Id &&
                    x.FinancialAccountId == walletId && x.Status != "Revoked" && !x.IsDeleted, ct);
                if (count >= InteracSecurity.MaximumSendingEmails) throw new InvalidOperationException("This wallet already has five sending emails. Remove one before adding another.");
            }
            row ??= new InteracSendingEmail { RouteId = route.Id, FinancialAccountId = walletId,
                Email = normalized, NormalizedEmail = normalized, CreatedByUserId = actor };
            if (db.Entry(row).State == EntityState.Detached) db.Add(row);
            row.IsDeleted = false; row.RevokedAt = null; row.IsDefault = false;
            await StageCodeAsync(row, actor, ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return await GetAsync(actor, walletId, business, ct);
    }

    public async Task<InteracFundingDto> ResendAsync(Guid actor, Guid walletId, bool business, Guid senderId, CancellationToken ct)
    {
        await contexts.LoadAsync(actor, walletId, business, true, true, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
            var sender = await OwnedSenderAsync(walletId, senderId, ct);
            if (sender.Status is "Verified" or "Revoked") throw new InvalidOperationException("This sending email does not need a verification code.");
            if (sender.CanResendAt > DateTime.UtcNow) throw new InvalidOperationException("Wait until the resend time before requesting another code.");
            var route = await db.Set<InteracReceivingRoute>().SingleAsync(x => x.Id == sender.RouteId, ct);
            EnsureCurrentActiveRoute(route, business);
            await StageCodeAsync(sender, actor, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return await GetAsync(actor, walletId, business, ct);
    }

    public async Task<InteracFundingDto> VerifyAsync(Guid actor, Guid walletId, bool business, Guid senderId, string code, CancellationToken ct)
    {
        await contexts.LoadAsync(actor, walletId, business, true, true, ct);
        string? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
            var sender = await OwnedSenderAsync(walletId, senderId, ct);
            var route = await db.Set<InteracReceivingRoute>().SingleAsync(x => x.Id == sender.RouteId, ct);
            EnsureCurrentActiveRoute(route, business);
            var now = DateTime.UtcNow;
            if (sender.Status != "Pending" || sender.CodeExpiresAt <= now || sender.FailedAttempts >= InteracSecurity.MaximumCodeAttempts)
                failure = "The verification code is expired, already used, or unavailable. Request a new code.";
            else if (!InteracSecurity.CodeMatches(sender, code?.Trim(), now))
            {
                sender.FailedAttempts++; sender.Revision = Guid.NewGuid();
                if (sender.FailedAttempts >= InteracSecurity.MaximumCodeAttempts) { sender.Status = "Expired"; InteracSecurity.ClearCode(sender); }
                failure = "The verification code is invalid. After five unsuccessful attempts, request a new code.";
            }
            else
            {
                await InteracSecurity.LockAsync(db, $"InteracSender:{route.Id}:{sender.NormalizedEmail}", ct);
                var claim = await db.Set<InteracSenderOwnership>().SingleOrDefaultAsync(x => x.RouteId == route.Id && x.NormalizedEmail == sender.NormalizedEmail, ct);
                var defaultWallet = await FindVerifiedPersonalDefaultWalletAsync(sender.NormalizedEmail, ct);
                if (claim is not null && claim.FinancialAccountId != walletId ||
                    defaultWallet.HasValue && defaultWallet.Value != walletId && claim is null)
                {
                    failure = "This email is already assigned to another wallet on this receiving route. Verify a different sending email or contact support.";
                    sender.Status = "Expired"; InteracSecurity.ClearCode(sender);
                }
                else
                {
                    if (claim is null) db.Add(new InteracSenderOwnership { RouteId = route.Id, NormalizedEmail = sender.NormalizedEmail,
                        FinancialAccountId = walletId, EstablishedAt = now });
                    sender.Status = "Verified"; sender.VerifiedAt = now; sender.RevokedAt = null;
                    InteracSecurity.ClearCode(sender);
                    InteracSecurity.Audit(db, actor, "SENDER_VERIFIED", nameof(InteracSendingEmail), sender.Id, "The nominated email was verified using a single-use code.");
                }
            }
            // Persist unsuccessful attempts before reporting an error; an exception inside
            // a rolled-back transaction must not give an attacker unlimited guesses.
            if (failure is not null) InteracSecurity.Audit(db, actor, "SENDER_VERIFICATION_REJECTED", nameof(InteracSendingEmail), sender.Id, failure);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        if (failure is not null) throw new InvalidOperationException(failure);
        return await GetAsync(actor, walletId, business, ct);
    }

    public async Task<InteracFundingDto> RemoveAsync(Guid actor, Guid walletId, bool business, Guid senderId, CancellationToken ct)
    {
        await contexts.LoadAsync(actor, walletId, business, true, false, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
            var sender = await OwnedSenderAsync(walletId, senderId, ct);
            sender.Status = "Revoked"; sender.RevokedAt = DateTime.UtcNow; InteracSecurity.ClearCode(sender);
            InteracSecurity.Audit(db, actor, "SENDER_REMOVED", nameof(InteracSendingEmail), sender.Id,
                "Sending stopped; historical ownership is retained and cannot be transferred to another wallet.");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return await GetAsync(actor, walletId, business, ct);
    }

    private async Task RevokeStaleDefaultsAsync(Guid actor, Guid walletId, Guid routeId, string? verifiedEmail, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
        var stale = await db.Set<InteracSendingEmail>().Where(x => x.RouteId == routeId && x.FinancialAccountId == walletId &&
            x.IsDefault && x.Status == "Verified" && (verifiedEmail == null || x.NormalizedEmail != verifiedEmail)).ToListAsync(ct);
        foreach (var sender in stale)
        {
            sender.Status = "Revoked"; sender.RevokedAt = DateTime.UtcNow; InteracSecurity.ClearCode(sender);
            InteracSecurity.Audit(db, actor, "DEFAULT_SENDER_REVOKED", nameof(InteracSendingEmail), sender.Id, "Registration email changed or is no longer verified.");
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task<string> EnsureDefaultAsync(Guid actor, Guid walletId, Guid routeId, string email, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + walletId, ct);
        await InteracSecurity.LockAsync(db, $"InteracSender:{routeId}:{email}", ct);
        var currentUser = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor, ct);
        if (!currentUser.EmailConfirmed || InteracSecurity.OptionalEmail(currentUser.Email) != email) return "Unverified";
        var obsolete = await db.Set<InteracSendingEmail>().Where(x => x.RouteId == routeId && x.FinancialAccountId == walletId &&
            x.IsDefault && x.NormalizedEmail != email && x.Status != "Revoked").ToListAsync(ct);
        foreach (var old in obsolete) { old.Status = "Revoked"; old.RevokedAt = DateTime.UtcNow; InteracSecurity.ClearCode(old); }
        var ownership = await db.Set<InteracSenderOwnership>().SingleOrDefaultAsync(x => x.RouteId == routeId && x.NormalizedEmail == email, ct);
        if (ownership is not null && ownership.FinancialAccountId != walletId)
        { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "Conflict"; }
        var row = await db.Set<InteracSendingEmail>().SingleOrDefaultAsync(x => x.RouteId == routeId &&
            x.FinancialAccountId == walletId && x.NormalizedEmail == email, ct);
        if (row?.Status == "Revoked")
        { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "Revoked"; }
        if (row?.Status == "Verified") { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "Verified"; }
        if (row is null && await db.Set<InteracSendingEmail>().CountAsync(x => x.RouteId == routeId &&
                x.FinancialAccountId == walletId && x.Status != "Revoked" && !x.IsDeleted, ct) >= InteracSecurity.MaximumSendingEmails)
        { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "LimitReached"; }
        var now = DateTime.UtcNow;
        if (ownership is null) db.Add(new InteracSenderOwnership { RouteId = routeId, NormalizedEmail = email, FinancialAccountId = walletId, EstablishedAt = now });
        if (row is null) { row = new InteracSendingEmail { RouteId = routeId, FinancialAccountId = walletId, Email = email, NormalizedEmail = email, CreatedByUserId = actor }; db.Add(row); }
        row.IsDefault = true; row.Status = "Verified"; row.VerifiedAt = now; InteracSecurity.ClearCode(row);
        InteracSecurity.Audit(db, actor, "DEFAULT_SENDER_ACTIVATED", nameof(InteracSendingEmail), row.Id, "Current verified registration email selected for the personal CAD wallet.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "Verified";
    }

    private Task<Guid?> FindVerifiedPersonalDefaultWalletAsync(string email, CancellationToken ct)
    {
        var normalized = email.ToUpperInvariant();
        return (from wallet in db.FinancialAccounts.AsNoTracking()
                join user in db.Users.AsNoTracking() on wallet.OwnerId equals user.Id
                where wallet.OwnerType == FinancialAccountOwnerType.User && wallet.AccountType == FinancialAccountType.Customer &&
                    wallet.AssetCode == "CAD" && !wallet.IsDeleted && user.EmailConfirmed && user.NormalizedEmail == normalized
                orderby wallet.CreatedAt
                select (Guid?)wallet.Id).FirstOrDefaultAsync(ct);
    }

    private async Task StageCodeAsync(InteracSendingEmail sender, Guid actor, CancellationToken ct)
    {
        var code = InteracSecurity.IssueCode(sender, DateTime.UtcNow);
        sender.LastUpdatedAt = DateTime.UtcNow; sender.LastUpdatedByUserId = actor;
        await notifications.QueueEmailAsync(sender.Email, "Verify your KorridorX Interac sending email",
            $"<p>Your KorridorX Interac sending-email verification code is <strong>{HtmlEncoder.Default.Encode(code)}</strong>.</p>" +
            "<p>It expires in 10 minutes and can be used once. A newer code replaces this one. Never share it with anyone.</p>" +
            "<p>This verifies where you send money from; it does not initiate a payment. Ignore this email if you did not request it.</p>",
            NotificationSecurityPolicy.AccountSecurityEntityType, sender.Id, ct);
        InteracSecurity.Audit(db, actor, "SENDER_CODE_QUEUED", nameof(InteracSendingEmail), sender.Id, "Verification email queued. Codes are excluded from API and audit output.");
    }

    private async Task<InteracSendingEmail> OwnedSenderAsync(Guid walletId, Guid id, CancellationToken ct) =>
        await db.Set<InteracSendingEmail>().SingleOrDefaultAsync(x => x.Id == id && x.FinancialAccountId == walletId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Sending email not found.");

    private Task<InteracReceivingRoute?> CurrentRouteAsync(bool business, CancellationToken ct) =>
        db.Set<InteracReceivingRoute>().AsNoTracking().Where(x => x.Environment == connection.EnvironmentName &&
            x.ConnectionKey == connection.ConnectionKey && !x.IsDeleted && (business ? x.BusinessEnabled : x.IndividualEnabled))
            .OrderByDescending(x => x.Status == "Active").ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    private void EnsureCurrentActiveRoute(InteracReceivingRoute route, bool business)
    {
        if (route.Environment != connection.EnvironmentName || route.ConnectionKey != connection.ConnectionKey || route.Status != "Active" ||
            (business ? !route.BusinessEnabled : !route.IndividualEnabled)) throw new InvalidOperationException("This receiving route is unavailable for the current provider connection.");
    }

    private static InteracFundingDto Empty(Guid wallet, string asset, string message) =>
        new(wallet, asset, false, null, null, "Unavailable", message, InteracSecurity.MaximumSendingEmails, null, null, [], []);
}
