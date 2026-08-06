using System.Text.Json;
using KorridorX.Data;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Reconciliation;

public class ProviderReconciliationService : IProviderReconciliationService
{
    private static readonly string[] TerminalProviderStatuses =
    [
        "SUCCESSFUL", "COMPLETED", "FAILED", "REJECTED", "EXPIRED", "REFUNDED", "REVERSED"
    ];

    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;
    private readonly ICollectionStatusService _collectionStatusService;
    private readonly IPayoutStatusService _payoutStatusService;

    public ProviderReconciliationService(
        AppDbContext db,
        IRemittanceProvider provider,
        ICollectionStatusService collectionStatusService,
        IPayoutStatusService payoutStatusService)
    {
        _db = db;
        _provider = provider;
        _collectionStatusService = collectionStatusService;
        _payoutStatusService = payoutStatusService;
    }

    public async Task<ProviderReconciliationResult> ReconcilePendingAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 500);
        var cutoff = DateTime.UtcNow.AddMinutes(-2);

        var ids = await _db.ProviderTransactions
            .AsNoTracking()
            .Where(x =>
                x.ProviderCode == _provider.ProviderCode &&
                x.LastSyncedAt <= cutoff &&
                !x.IsDeleted &&
                !TerminalProviderStatuses.Contains(x.ProviderStatus.ToUpper()))
            .OrderBy(x => x.LastSyncedAt)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var updated = 0;
        var failed = 0;

        foreach (var id in ids)
        {
            try
            {
                if (await ReconcileOneInternalAsync(id, ct))
                {
                    updated++;
                }
            }
            catch (ProviderIntegrationException)
            {
                failed++;
                await TouchAsync(id, ct);
            }
            catch
            {
                failed++;
                await TouchAsync(id, ct);
            }
        }

        return new ProviderReconciliationResult(ids.Count, updated, failed);
    }

    public async Task<ProviderReconciliationItemResult> ReconcileOneAsync(
        Guid providerTransactionRowId,
        CancellationToken ct = default)
    {
        var updated = await ReconcileOneInternalAsync(providerTransactionRowId, ct);
        var row = await _db.ProviderTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == providerTransactionRowId && !x.IsDeleted, ct);

        if (row is null)
        {
            throw new InvalidOperationException("Provider transaction not found.");
        }

        return new ProviderReconciliationItemResult(
            row.Id,
            updated,
            row.ProviderStatus,
            row.LastSyncedAt);
    }

    private async Task<bool> ReconcileOneInternalAsync(Guid providerTransactionRowId, CancellationToken ct)
    {
        var providerTransaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x => x.Id == providerTransactionRowId && !x.IsDeleted, ct);

        if (providerTransaction is null)
        {
            return false;
        }

        if (providerTransaction.TransactionType.Equals("refund", StringComparison.OrdinalIgnoreCase))
        {
            return await ReconcileRefundAsync(providerTransaction, ct);
        }

        var result = await _provider.GetTransactionAsync(
            providerTransaction.ProviderTransactionId,
            providerTransaction.TransferId,
            providerTransaction.CollectionId,
            providerTransaction.PayoutId,
            ct);

        var changed = !string.Equals(
            providerTransaction.ProviderStatus,
            result.ProviderStatus,
            StringComparison.OrdinalIgnoreCase);

        providerTransaction.ProviderReference = result.ProviderReference ?? providerTransaction.ProviderReference;
        providerTransaction.ProviderStatus = result.ProviderStatus;
        providerTransaction.TransactionType = string.IsNullOrWhiteSpace(result.TransactionType)
            ? providerTransaction.TransactionType
            : result.TransactionType;
        providerTransaction.CurrencyCode = result.CurrencyCode;
        providerTransaction.Amount = result.Amount;
        providerTransaction.RawPayloadJson = result.RawResponseJson;
        providerTransaction.ProviderCreatedAt = result.ProviderCreatedAt ?? providerTransaction.ProviderCreatedAt;
        providerTransaction.LastSyncedAt = DateTime.UtcNow;
        providerTransaction.LastUpdatedAt = DateTime.UtcNow;

        if (providerTransaction.CollectionId is Guid collectionId)
        {
            await ApplyCollectionStatusAsync(collectionId, result, ct);
        }

        if (providerTransaction.PayoutId is Guid payoutId)
        {
            await ApplyPayoutStatusAsync(payoutId, result, ct);
        }

        await _db.SaveChangesAsync(ct);
        return changed;
    }

    private async Task<bool> ReconcileRefundAsync(
        ProviderTransaction providerTransaction,
        CancellationToken ct)
    {
        if (providerTransaction.CollectionId is not Guid collectionId)
        {
            throw new InvalidOperationException("Refund provider transaction is not linked to a collection.");
        }

        var result = await _provider.GetRefundAsync(
            providerTransaction.ProviderTransactionId,
            providerTransaction.TransferId,
            collectionId,
            ct);

        var changed = !string.Equals(
            providerTransaction.ProviderStatus,
            result.ProviderStatus,
            StringComparison.OrdinalIgnoreCase);

        providerTransaction.ProviderReference = result.ProviderRefundReference ?? result.ProviderReference;
        providerTransaction.ProviderStatus = result.ProviderStatus;
        providerTransaction.CurrencyCode = result.CurrencyCode;
        providerTransaction.Amount = result.Amount;
        providerTransaction.RawPayloadJson = result.RawResponseJson;
        providerTransaction.ProviderCreatedAt = result.ProviderCreatedAt ?? providerTransaction.ProviderCreatedAt;
        providerTransaction.LastSyncedAt = DateTime.UtcNow;
        providerTransaction.LastUpdatedAt = DateTime.UtcNow;

        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == collectionId && !x.IsDeleted, ct);

        if (collection is not null)
        {
            collection.ProviderRefundId = result.ProviderRefundId;
            collection.ProviderRefundReference = result.ProviderRefundReference ?? result.ProviderReference;
            collection.RefundFailureReason = result.FailureReason;
            collection.LastRefundSyncedAt = DateTime.UtcNow;

            var status = result.ProviderStatus.Trim().ToUpperInvariant() switch
            {
                "SUCCESSFUL" or "COMPLETED" => CollectionStatus.Refunded,
                "FAILED" or "REJECTED" => CollectionStatus.RefundFailed,
                _ => CollectionStatus.RefundPending
            };

            if (collection.Status == status || _collectionStatusService.CanTransition(collection.Status, status))
            {
                _collectionStatusService.ApplyTransition(
                    collection,
                    status,
                    new CollectionStatusTransitionContext(
                        Source: "Reconciliation",
                        Reason: result.FailureReason ?? $"Provider refund reconciled as {result.ProviderStatus}.",
                        ProviderResponseId: result.ProviderRefundId,
                        ResponsePayloadJson: result.RawResponseJson,
                        MetadataJson: JsonSerializer.Serialize(new
                        {
                            reconciliation = true,
                            providerRefundId = result.ProviderRefundId,
                            providerStatus = result.ProviderStatus
                        }),
                        OccurredAt: result.ProviderUpdatedAt ?? result.ProviderCreatedAt));
            }
        }

        await _db.SaveChangesAsync(ct);
        return changed;
    }

    private async Task ApplyCollectionStatusAsync(
        Guid collectionId,
        RemittanceTransactionStatusResult result,
        CancellationToken ct)
    {
        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == collectionId && !x.IsDeleted, ct);

        if (collection is null)
        {
            return;
        }

        var status = MapCollectionStatus(result.ProviderStatus);
        if (status is CollectionStatus.Successful or CollectionStatus.Refunded)
        {
            ValidateMoney(collection.Amount, collection.CurrencyCode, result.Amount, result.CurrencyCode);
        }

        var attempt = collection.Attempts.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();

        if (collection.Status == status ||
            _collectionStatusService.CanTransition(collection.Status, status))
        {
            _collectionStatusService.ApplyTransition(
                collection,
                status,
                new CollectionStatusTransitionContext(
                    Source: "Reconciliation",
                    Reason: result.FailureReason ?? $"Provider transaction reconciled as {result.ProviderStatus}.",
                    ProviderCollectionId: result.ProviderTransactionId,
                    ProviderReference: result.ProviderReference,
                    ProviderRequestId: result.ProviderRequestLogId.ToString(),
                    ProviderResponseId: result.ProviderTransactionId,
                    ResponsePayloadJson: result.RawResponseJson,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        reconciliation = true,
                        providerStatus = result.ProviderStatus
                    }),
                    OccurredAt: result.ProviderCreatedAt),
                attempt);
        }
    }

    private async Task ApplyPayoutStatusAsync(
        Guid payoutId,
        RemittanceTransactionStatusResult result,
        CancellationToken ct)
    {
        var payout = await _db.Payouts
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == payoutId && !x.IsDeleted, ct);

        if (payout is null)
        {
            return;
        }

        var status = MapPayoutStatus(result.ProviderStatus);
        if (status == PayoutStatus.Successful)
        {
            ValidateMoney(payout.Amount, payout.CurrencyCode, result.Amount, result.CurrencyCode);
        }

        var attempt = payout.Attempts.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();

        if (payout.Status == status ||
            _payoutStatusService.CanTransition(payout.Status, status))
        {
            _payoutStatusService.ApplyTransition(
                payout,
                status,
                new PayoutStatusTransitionContext(
                    Source: "Reconciliation",
                    Reason: result.FailureReason ?? $"Provider payout reconciled as {result.ProviderStatus}.",
                    ProviderPayoutId: result.ProviderTransactionId,
                    ProviderReference: result.ProviderReference,
                    ProviderRequestId: result.ProviderRequestLogId.ToString(),
                    ProviderResponseId: result.ProviderTransactionId,
                    ResponsePayloadJson: result.RawResponseJson,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        reconciliation = true,
                        providerStatus = result.ProviderStatus
                    }),
                    OccurredAt: result.ProviderCreatedAt),
                attempt);
        }
    }

    private async Task TouchAsync(Guid id, CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (transaction is null)
        {
            return;
        }

        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static CollectionStatus MapCollectionStatus(string status) =>
        status.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => CollectionStatus.Successful,
            "PROCESSING" => CollectionStatus.Processing,
            "FAILED" or "REJECTED" => CollectionStatus.Failed,
            "EXPIRED" => CollectionStatus.Expired,
            "REFUND_PENDING" or "REFUND_INITIATED" => CollectionStatus.RefundPending,
            "REFUNDED" => CollectionStatus.Refunded,
            _ => CollectionStatus.Initiated
        };

    private static PayoutStatus MapPayoutStatus(string status) =>
        status.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => PayoutStatus.Successful,
            "PROCESSING" => PayoutStatus.Processing,
            "FAILED" or "REJECTED" => PayoutStatus.Failed,
            "REVERSED" => PayoutStatus.Reversed,
            _ => PayoutStatus.Initiated
        };

    private static void ValidateMoney(
        decimal expectedAmount,
        string expectedCurrency,
        decimal actualAmount,
        string actualCurrency)
    {
        if (!string.Equals(expectedCurrency, actualCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider transaction currency mismatch. Expected {expectedCurrency}, received {actualCurrency}.");
        }

        if (Math.Abs(expectedAmount - actualAmount) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Provider transaction amount mismatch. Expected {expectedAmount}, received {actualAmount}.");
        }
    }
}
