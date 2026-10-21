using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Providers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Webhooks;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Audit;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Webhooks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Providers;

/// <summary>
/// Operator recovery of a retained, signed dedicated-bank receipt. The client supplies
/// neither a replacement payload nor a wallet/amount. Money still uses the webhook's
/// provider verification, transaction lock, claim and ledger settlement implementation.
/// </summary>
public sealed class RetainedBankDepositRecoveryService(
    AppDbContext db, IProviderWalletResolver connection, IRemittanceProvider provider,
    WalletDepositAccountService deposits, IAuditService audit, IDataProtectionProvider protection,
    IOptions<BlaaizOptions> options, TimeProvider? clock = null)
{
    private readonly IDataProtector _protector = protection.CreateProtector("KorridorX.RetainedBankDepositRecovery.v1");
    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();

    public async Task<RetainedBankDepositPreviewDto> PreviewAsync(Guid webhookEventId, Guid actor,
        string reason, CancellationToken ct = default)
    {
        RequireActor(actor);
        reason = NormalizeReason(reason);
        var candidate = await ResolveAsync(webhookEventId, ct);
        var verified = await provider.GetTransactionAsync(candidate.Request.ProviderTransactionId, ct: ct);
        WalletDepositAccountService.ValidateVerifiedDeposit(candidate.Bank, candidate.Request, verified);
        var existing = await FindCompletedCollectionAsync(candidate.Bank, verified, ct);
        if (candidate.Receipt.ProcessingStatus == WebhookProcessingStatus.Processed && existing is null)
            throw new InvalidOperationException("This processed event has no matching completed bank deposit. Use reconciliation review.");
        var now = Now;
        var confirmation = new Confirmation(webhookEventId, actor, reason, now, now.AddMinutes(15),
            Fingerprint(candidate.Receipt, candidate.Bank, candidate.Wallet, verified),
            Hash(candidate.Receipt.RawPayloadJson), verified.ProviderTransactionId);
        var token = _protector.Protect(JsonSerializer.Serialize(confirmation));
        return new(webhookEventId, candidate.Receipt.ProviderEventId, verified.ProviderTransactionId,
            verified.ProviderReference, candidate.Bank.AssetCode, verified.Amount, verified.Amount,
            candidate.Wallet.Id, candidate.Wallet.AccountCode, candidate.Wallet.OwnerType.ToString(), candidate.Wallet.OwnerId,
            candidate.Bank.Id, candidate.Bank.ProviderAccountId!, candidate.Bank.AccountNumber, candidate.Bank.Iban,
            candidate.Bank.BankName, candidate.Bank.AccountName, existing is not null, existing?.Id, reason,
            confirmation.ExpiresAt, token);
    }

    public async Task<RetainedBankDepositRecoveryResultDto> ApplyAsync(Guid webhookEventId, Guid actor,
        string previewToken, CancellationToken ct = default)
    {
        RequireActor(actor);
        var confirmation = ReadConfirmation(webhookEventId, actor, previewToken);
        var attemptId = Guid.NewGuid();
        var startedAt = Now.UtcDateTime;
        try
        {
            var candidate = await ResolveAsync(webhookEventId, ct);
            if (Hash(candidate.Receipt.RawPayloadJson) != confirmation.PayloadHash)
                throw ChangedPreview();
            WebhookEvent? receipt = null;
            WebhookProcessingAttempt? attempt = null;
            WebhookProcessingStatus previousStatus = candidate.Receipt.ProcessingStatus;
            RetainedBankDepositRecoveryResultDto? result = null;
            var handled = await deposits.TryProcessDepositAsync(candidate.Request, new BankDepositRecoveryCallbacks(
                async (bank, wallet, verified, cancellation) =>
                {
                    // Same provider transaction and wallet locks as live settlement are
                    // already held. This row lock serializes receipt completion as well.
                    receipt = await db.WebhookEvents.FromSqlInterpolated(
                        $"SELECT * FROM \"WebhookEvents\" WHERE \"Id\" = {webhookEventId} FOR UPDATE").SingleAsync(cancellation);
                    await db.Entry(receipt).ReloadAsync(cancellation);
                    await ValidateReceiptAsync(receipt, cancellation);
                    if (Now >= confirmation.ExpiresAt ||
                        Fingerprint(receipt, bank, wallet, verified) != confirmation.Fingerprint)
                        throw ChangedPreview();
                    previousStatus = receipt.ProcessingStatus;
                    attempt = new WebhookProcessingAttempt { Id = attemptId, WebhookEventId = receipt.Id,
                        Status = WebhookProcessingStatus.Processing, StartedAt = startedAt };
                    db.WebhookProcessingAttempts.Add(attempt);
                },
                async (collection, alreadyCredited, cancellation) =>
                {
                    if (receipt is null || attempt is null) throw new InvalidOperationException("Recovery receipt was not locked.");
                    if (alreadyCredited)
                    {
                        // A known credit must include the original atomic provider claim
                        // and ledger. An incomplete/manual row is not proof of settlement.
                        var verified = new RemittanceTransactionStatusResult(candidate.Request.ProviderTransactionId,
                            collection.ProviderReference, "collection", "SUCCESSFUL", collection.CurrencyCode,
                            collection.Amount, null, null, "{}", Guid.Empty);
                        var existing = await FindCompletedCollectionAsync(candidate.Bank, verified, cancellation);
                        if (existing?.Id != collection.Id) throw new InvalidOperationException("The completed provider credit needs reconciliation review.");
                    }
                    else if (previousStatus == WebhookProcessingStatus.Processed)
                        throw new InvalidOperationException("A processed event without a matching completed bank deposit cannot be recovered automatically.");
                    var finishedAt = Now.UtcDateTime;
                    receipt.ProcessingStatus = WebhookProcessingStatus.Processed;
                    receipt.ProcessedAt = finishedAt;
                    receipt.ErrorMessage = null;
                    attempt.Status = WebhookProcessingStatus.Processed;
                    attempt.FinishedAt = finishedAt;
                    audit.Stage(new AuditRecordRequest("PROVIDER_BANK_DEPOSIT_RECOVERED", "ProviderOperations",
                        nameof(WebhookEvent), receipt.Id.ToString(),
                        OldValues: new { ProcessingStatus = previousStatus.ToString() },
                        NewValues: new { ProcessingStatus = "Processed", CollectionId = collection.Id, AlreadyCredited = alreadyCredited },
                        Metadata: new { confirmation.Reason, AttemptId = attempt.Id, receipt.ProviderEventId,
                            ProviderTransactionId = candidate.Request.ProviderTransactionId, collection.FinancialAccountId,
                            collection.CurrencyCode, collection.Amount, DepositAccountId = candidate.Bank.Id,
                            confirmation.PayloadHash }, UserId: actor));
                    result = new(receipt.Id, candidate.Request.ProviderTransactionId, collection.CurrencyCode,
                        collection.Amount, collection.Amount, collection.FinancialAccountId!.Value, collection.Id,
                        alreadyCredited, WebhookProcessingStatus.Processed,
                        alreadyCredited ? "The deposit was already credited. The retained receipt is now recorded as processed; no additional credit was made."
                            : "The verified bank deposit was credited and its retained receipt was recorded as processed.");
                }), ct);
            if (!handled || result is null)
                throw new InvalidOperationException("The receiving account is unavailable on the current provider connection. No recovery credit was applied.");
            return result;
        }
        catch (Exception ex)
        {
            // Settlement has rolled back before control returns. Clear ALL tracked
            // success metadata as well as money before saving the independent failure.
            db.ChangeTracker.Clear();
            await RecordFailureAsync(confirmation, attemptId, startedAt, ex, CancellationToken.None);
            throw;
        }
    }

    private async Task<Candidate> ResolveAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Retained provider webhook not found.");
        await ValidateReceiptAsync(receipt, ct);
        var request = BlaaizWebhookService.ReadRetainedBankDeposit(receipt);
        var bank = await db.WalletDepositAccounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey &&
            x.ProviderAccountId == request.ProviderAccountId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("The signed receiving account is not registered on the current provider connection. It cannot be reassigned during recovery.");
        if (bank.Status != "Active" || bank.AssetCode != request.CurrencyCode.ToUpperInvariant() ||
            !string.IsNullOrWhiteSpace(request.ProviderCustomerId) && request.ProviderCustomerId != bank.ProviderCustomerId ||
            !string.IsNullOrWhiteSpace(request.AccountNumber) && bank.AccountNumber is not null && request.AccountNumber != bank.AccountNumber)
            throw new InvalidOperationException("The retained deposit does not match an active receiving account.");
        var wallet = await db.FinancialAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bank.FinancialAccountId, ct);
        if (wallet is null || wallet.IsDeleted || wallet.Status != FinancialAccountStatus.Active || wallet.AssetCode != bank.AssetCode ||
            wallet.AccountType != FinancialAccountType.Customer || wallet.OwnerType is not (FinancialAccountOwnerType.User or FinancialAccountOwnerType.Business))
            throw new InvalidOperationException("The receiving wallet is unavailable; recovery cannot change its owner or currency.");
        var approved = wallet.OwnerType == FinancialAccountOwnerType.User
            ? await db.CustomerProfiles.AsNoTracking().AnyAsync(x => x.UserId == wallet.OwnerId && !x.IsDeleted && x.KycStatus == KycStatus.Approved, ct)
            : await db.BusinessProfiles.AsNoTracking().AnyAsync(x => x.Id == wallet.OwnerId && !x.IsDeleted && x.KybStatus == KybStatus.Approved, ct);
        if (!approved) throw new InvalidOperationException("The receiving owner requires compliance review before bank-deposit recovery.");
        return new(receipt, bank, wallet, request);
    }

    private async Task ValidateReceiptAsync(WebhookEvent receipt, CancellationToken ct)
    {
        if (!options.Value.IsEnabled) throw new InvalidOperationException("Blaaiz is not enabled on this connection.");
        if (receipt.IsDeleted || receipt.ProviderCode != ProviderCode.Blaaiz)
            throw new InvalidOperationException("An active retained Blaaiz webhook is required.");
        if (receipt.ProcessingStatus is not (WebhookProcessingStatus.Ignored or WebhookProcessingStatus.Failed or WebhookProcessingStatus.Processed))
            throw new InvalidOperationException("This webhook is pending or is being processed. Wait for its outcome before recovery.");
        // Retried webhooks retain ReceivedAt but replace the latest signature headers.
        // Use an actual persisted receipt/attempt time; never mint a replacement signature.
        var recordedTimes = await db.WebhookProcessingAttempts.AsNoTracking()
            .Where(x => x.WebhookEventId == receipt.Id).Select(x => x.StartedAt).ToListAsync(ct);
        recordedTimes.Add(receipt.ReceivedAt);
        var authentic = recordedTimes.Any(time => BlaaizWebhookSignature.IsValid(receipt.RawPayloadJson,
            receipt.SignatureHeader, receipt.TimestampHeader, options.Value.WebhookSigningSecret,
            TimeSpan.FromMinutes(options.Value.WebhookTimestampToleranceMinutes),
            new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc))));
        if (!authentic)
            throw new InvalidOperationException("The retained webhook signature cannot be authenticated with the current signing secret. Missing signature evidence or a rotated secret requires separate reconciliation review.");
    }

    private async Task<Collection?> FindCompletedCollectionAsync(WalletDepositAccount bank,
        RemittanceTransactionStatusResult verified, CancellationToken ct)
    {
        var transaction = await db.ProviderTransactions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == verified.ProviderTransactionId, ct);
        if (transaction is null)
        {
            if (await db.Collections.AsNoTracking().AnyAsync(x => x.ProviderCode.ToUpper() == "BLAAIZ" &&
                    x.ProviderCollectionId == verified.ProviderTransactionId, ct) ||
                await db.Set<ProviderCollectionCreditClaim>().AsNoTracking().AnyAsync(x =>
                    x.ProviderCode == "BLAAIZ" && x.ProviderTransactionId == verified.ProviderTransactionId &&
                    x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey, ct))
                throw new InvalidOperationException("This provider deposit already has a collection or credit claim requiring reconciliation review.");
            return null;
        }
        var collection = transaction.CollectionId is Guid collectionId
            ? await db.Collections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == collectionId, ct) : null;
        if (transaction.IsDeleted || collection is null)
            throw new InvalidOperationException("The provider transaction is already recorded without a matching completed bank-deposit credit. Use reconciliation review.");
        if (collection.IsDeleted || collection.Status != CollectionStatus.Successful ||
            !collection.ProviderCode.Equals("BLAAIZ", StringComparison.OrdinalIgnoreCase) ||
            collection.Purpose != PaymentOperationPurpose.AccountFunding || collection.TransferId.HasValue ||
            collection.PaymentMethod != PaymentMethod.VirtualAccount ||
            collection.FinancialAccountId != bank.FinancialAccountId || collection.RelatedEntityType != nameof(WalletDepositAccount) ||
            collection.RelatedEntityId != bank.Id || collection.Amount != verified.Amount || collection.CurrencyCode != bank.AssetCode ||
            collection.ProviderCollectionId != verified.ProviderTransactionId ||
            !await db.Set<ProviderCollectionCreditClaim>().AsNoTracking().AnyAsync(x =>
                x.ProviderCode == "BLAAIZ" && x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey &&
                x.ProviderTransactionId == verified.ProviderTransactionId && x.CollectionId == collection.Id &&
                x.FinancialAccountId == bank.FinancialAccountId && x.Amount == verified.Amount && x.CurrencyCode == bank.AssetCode, ct) ||
            !await db.LedgerTransactions.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.RelatedEntityId == collection.Id &&
                x.RelatedEntityType == nameof(Collection) && x.ContextEntityType == nameof(FinancialAccount) &&
                x.ContextEntityId == bank.FinancialAccountId && x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                x.Status == LedgerTransactionStatus.Posted && x.AssetCode == bank.AssetCode && x.Amount == verified.Amount &&
                x.ReversalOfTransactionId == null && x.ReversedByTransactionId == null && x.ReversedAt == null &&
                x.IdempotencyScope == "ProviderCollectionCredit:BLAAIZ" && x.IdempotencyKey == verified.ProviderTransactionId, ct))
            throw new InvalidOperationException("The provider transaction is already recorded without a matching completed bank-deposit credit. Use reconciliation review.");
        return collection;
    }

    private async Task RecordFailureAsync(Confirmation confirmation, Guid attemptId, DateTime startedAt,
        Exception error, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = await db.WebhookEvents.FromSqlInterpolated(
            $"SELECT * FROM \"WebhookEvents\" WHERE \"Id\" = {confirmation.WebhookEventId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (receipt is null) return;
        // If a transport/cancellation error followed a successful commit, preserve
        // that completed attempt; an idempotent retry can read its actual outcome.
        if (await db.WebhookProcessingAttempts.AsNoTracking().AnyAsync(x => x.Id == attemptId, ct)) return;
        var message = error.Message.Length <= 2000 ? error.Message : error.Message[..2000];
        var now = Now.UtcDateTime;
        // Never overwrite a concurrent live delivery or completed recovery.
        if (receipt.ProcessingStatus is WebhookProcessingStatus.Ignored or WebhookProcessingStatus.Failed)
        {
            receipt.ProcessingStatus = WebhookProcessingStatus.Failed;
            receipt.ErrorMessage = message;
            receipt.ProcessedAt = now;
        }
        db.WebhookProcessingAttempts.Add(new WebhookProcessingAttempt { Id = attemptId, WebhookEventId = receipt.Id,
            Status = WebhookProcessingStatus.Failed, StartedAt = startedAt, FinishedAt = now, ErrorMessage = message });
        audit.Stage(new AuditRecordRequest("PROVIDER_BANK_DEPOSIT_RECOVERY_FAILED", "ProviderOperations", nameof(WebhookEvent),
            receipt.Id.ToString(), Metadata: new { confirmation.Reason, AttemptId = attemptId,
                confirmation.ProviderTransactionId, confirmation.PayloadHash, Error = message }, UserId: confirmation.Actor));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private string Fingerprint(WebhookEvent receipt, WalletDepositAccount bank, FinancialAccount wallet,
        RemittanceTransactionStatusResult verified) => Hash(JsonSerializer.Serialize(new
        {
            receipt.Id, receipt.ProviderEventId, receipt.EventType, PayloadHash = Hash(receipt.RawPayloadJson),
            SignatureHash = Hash(receipt.SignatureHeader ?? ""), receipt.TimestampHeader, ReceivedAtTicks = receipt.ReceivedAt.Ticks,
            connection.EnvironmentName, connection.ConnectionKey, DepositAccountId = bank.Id, bank.FinancialAccountId,
            bank.ProviderWalletConfigurationId, bank.PaymentChannelId, bank.ProviderAccountId, bank.ProviderCustomerId,
            bank.ProviderWalletId, bank.AssetCode, bank.AccountNumber, bank.Iban, bank.AccountName, bank.BankName,
            wallet.OwnerType, wallet.OwnerId, wallet.AccountCode, verified.ProviderTransactionId,
            verified.ProviderReference, Currency = verified.CurrencyCode.ToUpperInvariant(), verified.Amount
        }));

    private Confirmation ReadConfirmation(Guid id, Guid actor, string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 16384) throw ChangedPreview();
            var value = JsonSerializer.Deserialize<Confirmation>(_protector.Unprotect(token));
            var now = Now;
            if (value is null || value.WebhookEventId != id || value.Actor != actor || value.ExpiresAt <= now ||
                value.IssuedAt > now.AddSeconds(30) || value.ExpiresAt != value.IssuedAt.AddMinutes(15) ||
                value.IssuedAt < now.AddMinutes(-15) || NormalizeReason(value.Reason) != value.Reason)
                throw ChangedPreview();
            return value;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw ChangedPreview(); }
    }

    private static void RequireActor(Guid actor)
    { if (actor == Guid.Empty) throw new UnauthorizedAccessException("An authenticated operator is required."); }
    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new InvalidOperationException("Enter an operator reason from 1 to 1000 characters.");
        return reason.Trim();
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static InvalidOperationException ChangedPreview() =>
        new("The recovery preview expired, changed, or belongs to a different operator. Preview this retained event again before confirming.");
    private sealed record Candidate(WebhookEvent Receipt, WalletDepositAccount Bank, FinancialAccount Wallet,
        EmbeddedInboundCollectionRequest Request);
    private sealed record Confirmation(Guid WebhookEventId, Guid Actor, string Reason, DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt, string Fingerprint, string PayloadHash, string ProviderTransactionId);
}
