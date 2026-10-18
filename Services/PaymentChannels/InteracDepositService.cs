using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

public sealed record InteracWebhookEvidence(string? TransactionId, string? Reference, string? Status,
    string? Currency, decimal? Gross, decimal? Net, decimal? Fee, string? SenderEmail, string? ReceivingEmail);

public sealed class InteracDepositService(AppDbContext db, IProviderWalletResolver connection, IBlaaizApiClient provider,
    ProviderCollectionCreditGuard creditGuard, ICollectionStatusService statuses)
{
    // The only caller is the signature-validated collection webhook route. Dedicated
    // bank-account handlers get their existing first refusal before this CAD handler.
    public async Task<bool> TryProcessAsync(string rawPayload, CancellationToken ct)
    {
        var evidence = ReadEvidence(rawPayload);
        if (!string.Equals(evidence.Currency, "CAD", StringComparison.OrdinalIgnoreCase)) return false;
        var transactionId = evidence.TransactionId;
        if (!string.IsNullOrWhiteSpace(transactionId) && await db.Collections.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
            x.ProviderCode.ToUpper() == "BLAAIZ" && x.PaymentMethod != PaymentMethod.Interac &&
            (x.ProviderCollectionId == transactionId || evidence.Reference != null && x.ProviderReference == evidence.Reference), ct))
            return false; // Preserve existing dedicated-bank/other collection replay behavior.
        var key = string.IsNullOrWhiteSpace(transactionId)
            ? "unidentified:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)))
            : InteracSecurity.Required(transactionId, 150, "Provider transaction ID");
        InteracDeposit receipt;
        await using (var receive = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await InteracSecurity.LockAsync(db, ProviderCollectionCreditGuard.LockKey(connection.ConnectionKey, key), ct);
            receipt = await db.Set<InteracDeposit>().SingleOrDefaultAsync(x => x.Environment == connection.EnvironmentName &&
                x.ConnectionKey == connection.ConnectionKey && x.ProviderTransactionId == key, ct)
                ?? new InteracDeposit { Environment = connection.EnvironmentName, ConnectionKey = connection.ConnectionKey,
                    ProviderTransactionId = key, ProviderReference = evidence.Reference, SenderEmail = evidence.SenderEmail,
                    ReceivingEmail = evidence.ReceivingEmail, GrossAmount = evidence.Gross, NetAmount = evidence.Net, FeeAmount = evidence.Fee,
                    OriginalWebhookJson = rawPayload };
            if (db.Entry(receipt).State == EntityState.Detached) db.Add(receipt);
            if (string.IsNullOrWhiteSpace(transactionId)) Hold(receipt, "Incoming CAD deposit did not identify an immutable provider transaction. Review the signed webhook evidence.");
            await db.SaveChangesAsync(ct); await receive.CommitAsync(ct);
        }
        // Audited review is sticky. A later verification/route change cannot silently
        // claim an earlier unmatched receipt. This release exposes notes, never credit actions.
        if (receipt.Status == "NeedsReview") return true;
        if (receipt.CreditedAt.HasValue || receipt.Status == "MatchedCollection")
        {
            var replayProblem = ValidateReplay(receipt, evidence);
            if (replayProblem is not null)
            {
                InteracSecurity.Audit(db, null, "DEPOSIT_REPLAY_CONFLICT", nameof(InteracDeposit), receipt.Id, replayProblem);
                db.Add(new InteracDepositReviewNote { DepositId = receipt.Id, Note = replayProblem, CreatedByUserId = Guid.Empty });
                await db.SaveChangesAsync(ct);
            }
            return true;
        }
        // Persist the initial receipt before provider I/O. A timeout remains retryable
        // as PendingVerification and can never lose the original signed evidence.
        var confirmed = await provider.GetTransactionAsync(key, ct: ct);
        var value = confirmed.Data?.Data;
        if (value is null)
        {
            await using var invalid = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await InteracSecurity.LockAsync(db, ProviderCollectionCreditGuard.LockKey(connection.ConnectionKey, key), ct);
            await db.Entry(receipt).ReloadAsync(ct);
            if (!receipt.CreditedAt.HasValue && receipt.Status != "MatchedCollection")
                Hold(receipt, "The provider returned no confirmed transaction data. The signed receipt is retained for review.");
            await db.SaveChangesAsync(ct); await invalid.CommitAsync(ct); return true;
        }
        creditGuard.Remember(confirmed);
        await using var settlement = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await InteracSecurity.LockAsync(db, ProviderCollectionCreditGuard.LockKey(connection.ConnectionKey, key), ct);
            await db.Entry(receipt).ReloadAsync(ct);
            if (receipt.CreditedAt.HasValue || receipt.Status is "MatchedCollection" or "NeedsReview")
            { await settlement.CommitAsync(ct); return true; }
            receipt.VerifiedProviderJson = confirmed.RawResponseJson; receipt.VerifiedAt = DateTime.UtcNow;
            receipt.ProviderReference = value.Reference; receipt.GrossAmount = value.Amount; receipt.NetAmount = value.AmountWithoutFee;
            receipt.FeeAmount = value.Fee; receipt.ProviderOccurredAt = AsUtc(value.Date);
            receipt.SenderEmail = InteracSecurity.OptionalEmail(value.SourceInformation?.CollectionEmail);
            receipt.ReceivingEmail = InteracSecurity.OptionalEmail(value.PayeeCollectionEmail);
            var basicProblem = ValidateConfirmation(evidence, value);
            if (basicProblem is not null) return await SaveHoldAsync(receipt, basicProblem, settlement, ct);
            if (value.Status.ToUpperInvariant() is not ("SUCCESSFUL" or "COMPLETED"))
            {
                receipt.Status = value.Status.ToUpperInvariant() is "FAILED" or "REJECTED" or "CANCELLED" or "EXPIRED" ? "NotCompleted" : "PendingVerification";
                await db.SaveChangesAsync(ct); await settlement.CommitAsync(ct); return false;
            }
            var matches = await db.Collections.Include(x => x.Transfer).Include(x => x.Attempts).Where(x => !x.IsDeleted &&
                x.ProviderCode.ToUpper() == "BLAAIZ" &&
                (x.ProviderCollectionId == key || value.Reference != null && x.ProviderReference == value.Reference)).ToListAsync(ct);
            var providerRow = await db.ProviderTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == key, ct);
            if (providerRow?.CollectionId is Guid linkedId && matches.All(x => x.Id != linkedId))
            {
                var linked = await db.Collections.Include(x => x.Transfer).Include(x => x.Attempts).SingleOrDefaultAsync(x => x.Id == linkedId, ct);
                if (linked is not null) matches.Add(linked);
            }
            if (matches.Count > 1) return await SaveHoldAsync(receipt, "The provider transaction/reference identifies multiple existing collections.", settlement, ct);
            if (matches.Count == 1)
            {
                var existing = matches[0];
                if (existing.IsDeleted || existing.PaymentMethod != PaymentMethod.Interac || existing.CurrencyCode != "CAD")
                    return await SaveHoldAsync(receipt, "The provider transaction is already linked to a different or unavailable payment route.", settlement, ct);
                try { ProviderCollectionCreditGuard.ValidateExistingCollection(existing, key, value); }
                catch (InvalidOperationException ex) { return await SaveHoldAsync(receipt, ex.Message, settlement, ct); }
                if (existing.Status != CollectionStatus.Successful && !statuses.CanTransition(existing.Status, CollectionStatus.Successful))
                    return await SaveHoldAsync(receipt, "The existing Money Request cannot be completed from its current state. Its receipt is retained for review.", settlement, ct);
                await statuses.ApplyTransitionAsync(existing, CollectionStatus.Successful, new CollectionStatusTransitionContext(
                    Source: "VerifiedInteracMoneyRequest", Reason: "The provider independently confirmed the existing Interac Money Request.",
                    ProviderCollectionId: key, ProviderReference: value.Reference, ResponsePayloadJson: confirmed.RawResponseJson,
                    OccurredAt: receipt.ProviderOccurredAt ?? receipt.ReceivedAt), existing.Attempts.OrderByDescending(x => x.AttemptedAt).FirstOrDefault(), ct);
                receipt.CollectionId = existing.Id; receipt.FinancialAccountId = existing.FinancialAccountId;
                receipt.Status = existing.FinancialAccountId.HasValue ? "Credited" : "MatchedCollection";
                receipt.CreditedAt = existing.FinancialAccountId.HasValue ? existing.ConfirmedAt ?? DateTime.UtcNow : null;
                InteracSecurity.Audit(db, null, "DEPOSIT_MATCHED_EXISTING_COLLECTION", nameof(InteracDeposit), receipt.Id,
                    "Existing request association and fee accounting preserved. The shared provider claim prevents unsolicited re-credit.");
                await db.SaveChangesAsync(ct); await settlement.CommitAsync(ct); return true;
            }
            if (providerRow is not null) return await SaveHoldAsync(receipt, "A provider transaction exists without an unambiguous collection association.", settlement, ct);
            var moneyProblem = ValidateUnsolicitedMoney(value);
            if (moneyProblem is not null) return await SaveHoldAsync(receipt, moneyProblem, settlement, ct);
            if (receipt.SenderEmail is null || receipt.ReceivingEmail is null)
                return await SaveHoldAsync(receipt, "Provider confirmation did not identify both the actual Interac sending email and receiving email.", settlement, ct);
            var route = await db.Set<InteracReceivingRoute>().SingleOrDefaultAsync(x => x.Environment == connection.EnvironmentName &&
                x.ConnectionKey == connection.ConnectionKey && x.NormalizedReceivingEmail == receipt.ReceivingEmail, ct);
            if (route is null || route.VerifiedAt is null || route.ProviderWalletId != value.BusinessWalletId || route.ProviderBusinessId != value.BusinessId)
                return await SaveHoldAsync(receipt, "No provider-verified receiving route matches the confirmed CAD wallet, business and receiving email.", settlement, ct);
            receipt.RouteId = route.Id;
            // Pausing a route hides new instructions, but valid deposits already sent to
            // that verified route must still reconcile against its retained ownership.
            var sender = await db.Set<InteracSendingEmail>().AsNoTracking().SingleOrDefaultAsync(x => x.RouteId == route.Id &&
                x.NormalizedEmail == receipt.SenderEmail && x.Status == "Verified" && !x.IsDeleted, ct);
            var ownership = await db.Set<InteracSenderOwnership>().AsNoTracking().SingleOrDefaultAsync(x => x.RouteId == route.Id &&
                x.NormalizedEmail == receipt.SenderEmail, ct);
            if (sender is null || ownership is null || sender.FinancialAccountId != ownership.FinancialAccountId)
                return await SaveHoldAsync(receipt, "No verified sending-email owner matches this deposit. Do not infer ownership from a name, amount, or login.", settlement, ct);
            if (!receipt.ProviderOccurredAt.HasValue || !sender.VerifiedAt.HasValue ||
                receipt.ProviderOccurredAt.Value < sender.VerifiedAt.Value || receipt.ProviderOccurredAt.Value < ownership.EstablishedAt ||
                receipt.ProviderOccurredAt.Value > receipt.ReceivedAt.AddMinutes(5))
                return await SaveHoldAsync(receipt, "The deposit time is missing, implausible, or predates verified sender ownership. Historical deposits require reviewed resolution.", settlement, ct);
            await InteracSecurity.LockAsync(db, "InteracSenderWallet:" + sender.FinancialAccountId, ct);
            // Recheck under the same lock used by removal, resend, and verification.
            var stillVerified = await db.Set<InteracSendingEmail>().AsNoTracking().SingleAsync(x => x.Id == sender.Id, ct);
            if (stillVerified.Status != "Verified" || stillVerified.VerifiedAt != sender.VerifiedAt)
                return await SaveHoldAsync(receipt, "Sending-email authorization changed while processing this deposit.", settlement, ct);
            var wallet = await db.FinancialAccounts.FromSqlInterpolated($"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {sender.FinancialAccountId} FOR UPDATE").SingleAsync(ct);
            await db.Entry(wallet).ReloadAsync(ct);
            receipt.FinancialAccountId = wallet.Id;
            var walletProblem = await ValidateWalletAsync(wallet, sender, ct);
            if (walletProblem is not null) return await SaveHoldAsync(receipt, walletProblem, settlement, ct);
            var collection = new Collection { Purpose = PaymentOperationPurpose.AccountFunding, FinancialAccountId = wallet.Id,
                FinancialAccount = wallet, RelatedEntityType = nameof(InteracDeposit), RelatedEntityId = receipt.Id,
                ContextEntityType = nameof(FinancialAccount), ContextEntityId = wallet.Id, Reference = $"COL-INT-{Guid.NewGuid():N}",
                CurrencyCode = "CAD", Amount = value.AmountWithoutFee!.Value, PaymentMethod = PaymentMethod.Interac,
                PaymentChannelName = "Interac receiving email", Status = CollectionStatus.Initiated, ProviderCode = "BLAAIZ",
                ProviderCollectionId = key, ProviderReference = value.Reference, InitiatedAt = receipt.ReceivedAt };
            db.Add(collection);
            await statuses.ApplyTransitionAsync(collection, CollectionStatus.Successful, new CollectionStatusTransitionContext(
                Source: "VerifiedInteracDeposit", Reason: "Provider-confirmed Interac net deposit matched the verified sending-email owner.",
                ProviderCollectionId: key, ProviderReference: value.Reference, ResponsePayloadJson: confirmed.RawResponseJson,
                OccurredAt: receipt.ProviderOccurredAt), ct: ct);
            receipt.CollectionId = collection.Id; receipt.Status = "Credited"; receipt.CreditedAt = DateTime.UtcNow; receipt.ReviewReason = null;
            InteracSecurity.Audit(db, null, "DEPOSIT_CREDITED", nameof(InteracDeposit), receipt.Id,
                "The provider-confirmed net amount was credited once to the verified sender's wallet without creating a transfer.");
            await db.SaveChangesAsync(ct); await settlement.CommitAsync(ct); return true;
        }
        catch
        {
            await settlement.RollbackAsync(CancellationToken.None);
            await ProviderCollectionCreditGuard.RestoreMoneyTrackingAsync(db);
            throw;
        }
    }

    private async Task<string?> ValidateWalletAsync(FinancialAccount wallet, InteracSendingEmail sender, CancellationToken ct)
    {
        if (wallet.IsDeleted || wallet.Status != FinancialAccountStatus.Active || wallet.AssetCode != "CAD" ||
            wallet.AccountType != FinancialAccountType.Customer || wallet.OwnerType is not (FinancialAccountOwnerType.User or FinancialAccountOwnerType.Business))
            return "The identified CAD wallet is unavailable for deposit credit.";
        string? country;
        if (wallet.OwnerType == FinancialAccountOwnerType.User)
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == wallet.OwnerId, ct);
            if (user is null || user.Status != UserStatus.Active) return "The identified wallet owner is not active.";
            if (sender.IsDefault && (!user.EmailConfirmed || InteracSecurity.OptionalEmail(user.Email) != sender.NormalizedEmail))
                return "The personal default sending email is no longer the current verified registration email.";
            var profile = await db.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == wallet.OwnerId && !x.IsDeleted, ct);
            if (profile?.KycStatus != KycStatus.Approved) return "The personal wallet owner requires compliance review before this deposit can be credited.";
            country = profile.CountryCode;
        }
        else
        {
            var profile = await db.BusinessProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == wallet.OwnerId && !x.IsDeleted, ct);
            if (profile?.KybStatus != KybStatus.Approved) return "The business wallet owner requires compliance review before this deposit can be credited.";
            country = profile.CountryCode;
        }
        var business = wallet.OwnerType == FinancialAccountOwnerType.Business;
        if (!await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == country && x.AssetCode == "CAD" && x.CanDeposit &&
            x.Country.IsSupported && x.Asset.IsSupported && x.Asset.DepositEnabled && (business ? x.Asset.BusinessEnabled : x.Asset.IndividualEnabled), ct))
            return "CAD deposits are not enabled for this wallet's configured country and audience.";
        return null;
    }

    public static string? ValidateConfirmation(InteracWebhookEvidence evidence, BlaaizTransactionData value)
    {
        if (value.Id != evidence.TransactionId || !value.Type.Equals("collection", StringComparison.OrdinalIgnoreCase) || value.Currency.ToUpperInvariant() != "CAD")
            return "The provider confirmation did not match the signed CAD collection identity and type.";
        if (evidence.Gross.HasValue && evidence.Gross != value.Amount || evidence.Net.HasValue && evidence.Net != value.AmountWithoutFee ||
            evidence.Fee.HasValue && evidence.Fee != value.Fee)
            return "Signed webhook amounts disagree with independent provider confirmation.";
        if (evidence.Reference is not null && value.Reference is not null && evidence.Reference != value.Reference)
            return "Signed webhook reference disagrees with independent provider confirmation.";
        if (evidence.SenderEmail is not null && evidence.SenderEmail != InteracSecurity.OptionalEmail(value.SourceInformation?.CollectionEmail) ||
            evidence.ReceivingEmail is not null && evidence.ReceivingEmail != InteracSecurity.OptionalEmail(value.PayeeCollectionEmail))
            return "Signed webhook sender or receiving email disagrees with independent provider confirmation.";
        if (!string.IsNullOrWhiteSpace(value.RefundStatus) && value.RefundStatus.ToUpperInvariant() is not ("NONE" or "NOT_REFUNDED"))
            return "The provider transaction has a refund state and requires reviewed reconciliation before credit.";
        return null;
    }

    public static string? ValidateUnsolicitedMoney(BlaaizTransactionData value)
    {
        if (value.Amount <= 0 || value.AmountWithoutFee is null or <= 0 || value.Fee is null or < 0 ||
            value.AmountWithoutFee + value.Fee != value.Amount || decimal.Round(value.Amount, 2) != value.Amount ||
            decimal.Round(value.AmountWithoutFee.Value, 2) != value.AmountWithoutFee.Value || decimal.Round(value.Fee.Value, 2) != value.Fee.Value)
            return "The provider must confirm positive CAD gross/net amounts, a nonnegative fee, and gross = net + fee at valid CAD precision.";
        return null;
    }

    public static InteracWebhookEvidence ReadEvidence(string raw)
    {
        using var doc = JsonDocument.Parse(raw); var root = doc.RootElement;
        var data = root.TryGetProperty("data", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object ? wrapped : root;
        string? Text(params string[] keys) { foreach (var key in keys) foreach (var node in new[] { data, root })
            if (node.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString())) return x.GetString()!.Trim(); return null; }
        decimal? Money(params string[] keys) { foreach (var key in keys) foreach (var node in new[] { data, root })
            if (node.TryGetProperty(key, out var x) && (x.ValueKind == JsonValueKind.Number && x.TryGetDecimal(out var a))) return a;
            else if (node.TryGetProperty(key, out x) && x.ValueKind == JsonValueKind.String && decimal.TryParse(x.GetString(),
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var b)) return b; return null; }
        string? sender = null;
        if (data.TryGetProperty("source_information", out var source) && source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty("collection_email", out var senderValue) && senderValue.ValueKind == JsonValueKind.String) sender = senderValue.GetString();
        return new(Text("transaction_id", "provider_transaction_id", "collection_id"), Text("transaction_reference", "reference"),
            Text("transaction_status", "status"), Text("transaction_currency", "currency"), Money("transaction_amount", "amount"),
            Money("transaction_amount_without_fee", "amount_without_fee"), Money("transaction_fee", "fee"),
            InteracSecurity.OptionalEmail(sender), InteracSecurity.OptionalEmail(Text("payee_collection_email")));
    }

    private static DateTime? AsUtc(DateTime? value) => !value.HasValue ? null : value.Value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value.Value.ToUniversalTime();
    private static string? ValidateReplay(InteracDeposit receipt, InteracWebhookEvidence value) =>
        value.Gross.HasValue && receipt.GrossAmount != value.Gross || value.Net.HasValue && receipt.NetAmount != value.Net ||
        value.Fee.HasValue && receipt.FeeAmount != value.Fee || value.SenderEmail is not null && value.SenderEmail != receipt.SenderEmail ||
        value.ReceivingEmail is not null && value.ReceivingEmail != receipt.ReceivingEmail
            ? "A replay of an already processed provider transaction supplied conflicting deposit details. The original credit remains unchanged; review the signed receipts." : null;
    private void Hold(InteracDeposit receipt, string reason)
    {
        receipt.Status = "NeedsReview"; receipt.ReviewReason = reason; receipt.LastUpdatedAt = DateTime.UtcNow;
        InteracSecurity.Audit(db, null, "DEPOSIT_HELD_FOR_REVIEW", nameof(InteracDeposit), receipt.Id, reason);
    }
    private async Task<bool> SaveHoldAsync(InteracDeposit receipt, string reason, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken ct)
    { Hold(receipt, reason); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
}
