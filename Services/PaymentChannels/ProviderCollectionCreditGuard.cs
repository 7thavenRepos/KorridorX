using KorridorX.Data;
using KorridorX.Models.Audit;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

public sealed class ProviderCollectionCreditGuard(AppDbContext db, IProviderWalletResolver connection, IBlaaizApiClient provider)
{
    private readonly Dictionary<string, BlaaizApiResult<BlaaizTransactionEnvelope>> _confirmed = new(StringComparer.Ordinal);
    public static string LockKey(string connectionKey, string transactionId) => "ProviderCollection:BLAAIZ:" + connectionKey + ":" + transactionId;
    public static bool Applies(Collection collection, string? incomingId) =>
        collection.ProviderCode.Equals("BLAAIZ", StringComparison.OrdinalIgnoreCase);
    public void Remember(BlaaizApiResult<BlaaizTransactionEnvelope> result) => _confirmed[result.Data.Data.Id] = result;

    // Caller holds a database transaction through collection/ledger SaveChanges.
    // Every completion path uses this same provider transaction lock and claim.
    public async Task PrepareAsync(Collection collection, string? incomingId, CancellationToken ct)
    {
        if (!Applies(collection, incomingId)) return;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Collection credit must be performed within its settlement transaction.");
        var transactionId = InteracSecurity.Required(string.IsNullOrWhiteSpace(incomingId) ? collection.ProviderCollectionId : incomingId, 150, "Provider transaction ID");
        await InteracSecurity.LockAsync(db, LockKey(connection.ConnectionKey, transactionId), ct);
        if (db.Entry(collection).State != EntityState.Added) await db.Entry(collection).ReloadAsync(ct);
        if (collection.ProviderCollectionId is { Length: > 0 } && collection.ProviderCollectionId != transactionId)
            throw new InvalidOperationException("The provider transaction does not match this existing collection.");
        var existing = await db.ProviderTransactions.SingleOrDefaultAsync(x => x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == transactionId, ct);
        if (existing?.CollectionId is Guid assigned && assigned != collection.Id)
            throw new InvalidOperationException("This provider deposit already belongs to another collection. No additional credit is allowed.");
        if (await db.Collections.AsNoTracking().AnyAsync(x => x.Id != collection.Id && x.ProviderCollectionId == transactionId &&
            x.ProviderCode.ToUpper() == "BLAAIZ", ct))
            throw new InvalidOperationException("The provider transaction identifies multiple collections. Retain it for audited review.");
        var claim = db.Set<ProviderCollectionCreditClaim>().Local.FirstOrDefault(x => x.ProviderCode == "BLAAIZ" &&
            x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey && x.ProviderTransactionId == transactionId)
            ?? await db.Set<ProviderCollectionCreditClaim>().SingleOrDefaultAsync(x => x.ProviderCode == "BLAAIZ" &&
                x.Environment == connection.EnvironmentName && x.ConnectionKey == connection.ConnectionKey && x.ProviderTransactionId == transactionId, ct);
        if (claim is not null && (claim.CollectionId != collection.Id || claim.FinancialAccountId != collection.FinancialAccountId ||
            claim.Amount != collection.Amount || claim.CurrencyCode != collection.CurrencyCode))
            throw new InvalidOperationException("This provider deposit was claimed with different wallet, collection, amount or currency details.");
        if (collection.PaymentMethod == PaymentMethod.Interac && collection.CurrencyCode == "CAD")
        {
            if (!_confirmed.TryGetValue(transactionId, out var result)) result = await provider.GetTransactionAsync(transactionId, collection.TransferId, collection.Id, ct: ct);
            var value = result.Data.Data;
            ValidateExistingCollection(collection, transactionId, value);
            var selection = await db.ProviderWalletSelections.AsNoTracking().Include(x => x.WalletConfiguration)
                .SingleOrDefaultAsync(x => x.OperationType == "Collection" && x.OperationId == collection.Id, ct);
            if (selection is not null && (selection.WalletConfiguration.Environment != connection.EnvironmentName ||
                selection.WalletConfiguration.VerifiedConnectionKey != connection.ConnectionKey ||
                value.BusinessWalletId != selection.ProviderWalletId))
                throw new InvalidOperationException("The confirmed Interac payment does not match the provider wallet and connection pinned to its Money Request.");
            existing ??= new ProviderTransaction { ProviderCode = ProviderCode.Blaaiz, ProviderTransactionId = transactionId };
            if (db.Entry(existing).State == EntityState.Detached) db.Add(existing);
            existing.CollectionId = collection.Id; existing.Collection = collection; existing.TransferId = collection.TransferId;
            existing.ProviderReference = value.Reference; existing.TransactionType = "collection"; existing.ProviderStatus = value.Status;
            existing.CurrencyCode = "CAD"; existing.Amount = value.Amount; existing.AmountWithoutFee = value.AmountWithoutFee;
            existing.ProviderFeeAmount = value.Fee; existing.ProviderFeeCurrencyCode = "CAD"; existing.ProviderCreatedAt = value.Date;
            existing.RawPayloadJson = result.RawResponseJson; existing.LastSyncedAt = DateTime.UtcNow;
        }
        if (collection.FinancialAccountId is Guid walletId)
        {
            var wallet = await db.FinancialAccounts.FromSqlInterpolated($"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {walletId} FOR UPDATE").SingleAsync(ct);
            await db.Entry(wallet).ReloadAsync(ct);
            if (wallet.IsDeleted || wallet.Status != FinancialAccountStatus.Active || wallet.AssetCode != collection.CurrencyCode)
                throw new InvalidOperationException("The receiving wallet is unavailable; no collection credit was applied.");
        }
        if (claim is null) db.Add(new ProviderCollectionCreditClaim { Environment = connection.EnvironmentName,
            ConnectionKey = connection.ConnectionKey, ProviderTransactionId = transactionId, CollectionId = collection.Id,
            FinancialAccountId = collection.FinancialAccountId, CurrencyCode = collection.CurrencyCode, Amount = collection.Amount });
    }

    public static void ValidateExistingCollection(Collection collection, string transactionId, BlaaizTransactionData value)
    {
        var unsolicited = collection.RelatedEntityType == nameof(InteracDeposit);
        if (value is null || value.Id != transactionId || !value.Type.Equals("collection", StringComparison.OrdinalIgnoreCase) ||
            value.Status.ToUpperInvariant() is not ("SUCCESSFUL" or "COMPLETED") || value.Currency.ToUpperInvariant() != collection.CurrencyCode ||
            (unsolicited ? value.AmountWithoutFee != collection.Amount : value.Amount != collection.Amount) || collection.Amount <= 0)
            throw new InvalidOperationException("Provider confirmation does not match the existing collection ID, status, amount and currency.");
        if (!string.IsNullOrWhiteSpace(collection.ProviderReference) && !string.IsNullOrWhiteSpace(value.Reference) && collection.ProviderReference != value.Reference)
            throw new InvalidOperationException("The confirmed provider reference disagrees with the existing collection.");
        // Existing Money Requests retain their gross requested amount and existing fee
        // accounting. New unsolicited collections always use the confirmed net amount.
        if (value.AmountWithoutFee.HasValue && value.Fee.HasValue &&
            (value.AmountWithoutFee <= 0 || value.Fee < 0 || value.AmountWithoutFee + value.Fee != value.Amount))
            throw new InvalidOperationException("Provider gross, net and fee amounts are inconsistent.");
    }

    public static void ApplyLedgerIdentity(Collection collection, LedgerTransaction ledger)
    {
        if (!Applies(collection, null)) return;
        if (string.IsNullOrWhiteSpace(collection.ProviderCollectionId)) throw new InvalidOperationException("An immutable provider transaction ID is required before external credit.");
        ledger.IdempotencyScope = "ProviderCollectionCredit:BLAAIZ";
        ledger.IdempotencyKey = collection.ProviderCollectionId;
        ledger.IdempotencyRequestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{collection.Id:N}|{collection.FinancialAccountId:N}|{collection.CurrencyCode}|{collection.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));
    }

    public static async Task RestoreMoneyTrackingAsync(AppDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is Collection or CollectionAttempt or Transfer or TransferTimelineEvent or
            FinancialAccount or FinancialReservation or LedgerTransaction or LedgerPosting or ProviderTransaction or ProviderCollectionCreditClaim or InteracDeposit or
            BusinessWebhookEvent or BusinessWebhookDelivery ||
            x.Entity is NotificationMessage { RelatedEntityType: WalletActivityNotificationFactory.FundingEntityType } ||
            x.Entity is AuditLog { Category: "InteracFunding" }).ToArray())
        {
            if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
            else if (entry.State != EntityState.Detached) await entry.ReloadAsync(CancellationToken.None);
        }
    }
}
