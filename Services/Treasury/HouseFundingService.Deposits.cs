using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Treasury;

public sealed partial class HouseFundingService
{
    private sealed record ReservedDeposit(string Key, string Asset, decimal Amount);
    private sealed record DepositReview(List<ReservedDeposit> Items, int Unclassified, bool Unquantified, bool LimitReached);

    private async Task<DepositReview> UncreditedDepositsAsync(CancellationToken ct)
    {
        var candidates = new Dictionary<string, ReservedDeposit>(StringComparer.Ordinal);
        var unknown = new HashSet<string>(StringComparer.Ordinal);
        var unquantified = false;
        // Successful customer collections without an operational ledger credit
        // remain owed even if their delivery/credit processing failed.
        var collections = await db.Collections.AsNoTracking().Where(x => !x.IsDeleted &&
            x.Status == CollectionStatus.Successful && x.RefundedAt == null &&
            (x.FinancialAccountId != null && x.FinancialAccount!.AccountType == FinancialAccountType.Customer ||
             x.TransferId != null && x.Transfer!.Status != TransferStatus.Completed && x.Transfer.Status != TransferStatus.Refunded))
            .Select(x => new { x.Id, x.ProviderCollectionId, x.CurrencyCode, x.Amount }).ToListAsync(ct);
        var ids = collections.Select(x => x.Id).ToArray();
        var creditedCollections = await db.LedgerTransactions.AsNoTracking().Where(x => !x.IsDeleted &&
            x.RelatedEntityType == nameof(Collection) && x.RelatedEntityId.HasValue && ids.Contains(x.RelatedEntityId.Value) &&
            x.Status == LedgerTransactionStatus.Posted && x.ReversedAt == null &&
            (x.Type == LedgerTransactionType.ExternalCollectionCredit || x.Type == LedgerTransactionType.FundingCredit))
            .Select(x => x.RelatedEntityId!.Value).ToListAsync(ct);
        var creditedSet = creditedCollections.ToHashSet();
        foreach (var c in collections.Where(x => !creditedSet.Contains(x.Id)))
            Reserve(c.ProviderCollectionId ?? "collection:" + c.Id, c.CurrencyCode, c.Amount);

        const int maximumReceipts = 2000;
        var receipts = await db.WebhookEvents.AsNoTracking().Where(x => !x.IsDeleted &&
            x.ProviderCode == ProviderCode.Blaaiz && x.ProcessingStatus != WebhookProcessingStatus.Processed &&
            x.EventType == "collection.completed")
            .OrderBy(x => x.ReceivedAt).Take(maximumReceipts + 1).ToListAsync(ct);
        var knownAccounts = (await db.WalletDepositAccounts.AsNoTracking()
            .Where(x => x.ProviderAccountId != null).Select(x => x.ProviderAccountId!).ToListAsync(ct))
            .Concat(await db.ProviderAccountMappings.AsNoTracking()
                .Where(x => x.ProviderCode.ToUpper() == "BLAAIZ" && x.ProviderAccountId != null)
                .Select(x => x.ProviderAccountId!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in receipts.Take(maximumReceipts))
        {
            try
            {
                var r = BlaaizWebhookService.ReadRetainedBankDeposit(receipt);
                // Keep a successful incoming amount reserved even when its owner
                // is unknown. An unmatched deposit is not company funding.
                Reserve(r.ProviderTransactionId, r.CurrencyCode, r.Amount);
                if (r.ProviderAccountId is null || !knownAccounts.Contains(r.ProviderAccountId))
                    unknown.Add(r.ProviderTransactionId);
            }
            catch (Exception e) when (e is InvalidOperationException or JsonException or FormatException)
            { unknown.Add("receipt:" + receipt.Id); unquantified = true; }
        }
        // Retried/ignored receipts for an already credited transaction must not
        // reserve that amount again. The shared credit guard provides its identity.
        var transactionIds = candidates.Keys.Where(x => !x.StartsWith("collection:", StringComparison.Ordinal)).ToArray();
        var creditedKeys = await db.LedgerTransactions.AsNoTracking().Where(x => !x.IsDeleted &&
            x.Status == LedgerTransactionStatus.Posted && x.ReversedAt == null &&
            x.IdempotencyScope == "ProviderCollectionCredit:BLAAIZ" && x.IdempotencyKey != null && transactionIds.Contains(x.IdempotencyKey))
            .Select(x => x.IdempotencyKey!).ToListAsync(ct);
        foreach (var key in creditedKeys) { candidates.Remove(key); unknown.Remove(key); }
        return new(candidates.Values.ToList(), unknown.Count, unquantified, receipts.Count > maximumReceipts);

        void Reserve(string key, string asset, decimal amount)
        {
            if (amount <= 0m) return;
            var code = asset.Trim().ToUpperInvariant();
            if (candidates.TryGetValue(key, out var old))
            {
                if (old.Asset != code) { unknown.Add(key); unquantified = true; return; }
                amount = Math.Max(old.Amount, amount);
            }
            candidates[key] = new(key, code, amount);
        }
    }
}
