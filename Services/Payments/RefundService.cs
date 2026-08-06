using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Payments;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Payments;

public class RefundService : IRefundService
{
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "EUR", "GBP" };

    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;
    private readonly ICollectionStatusService _collectionStatusService;

    public RefundService(
        AppDbContext db,
        IRemittanceProvider provider,
        ICollectionStatusService collectionStatusService)
    {
        _db = db;
        _provider = provider;
        _collectionStatusService = collectionStatusService;
    }

    public async Task<RefundDto> InitiateAsync(
        Guid collectionId,
        string? reason,
        Guid changedByUserId,
        CancellationToken ct = default)
    {
        var collection = await LoadCollectionAsync(collectionId, ct);

        if (collection.Status == CollectionStatus.Refunded)
        {
            return ToDto(collection);
        }

        if (!string.IsNullOrWhiteSpace(collection.ProviderRefundId))
        {
            return await RefreshAsync(collectionId, changedByUserId, ct);
        }

        if (collection.Status != CollectionStatus.Successful)
        {
            throw new InvalidOperationException("Only a successful collection can be refunded.");
        }

        if (!SupportedCurrencies.Contains(collection.CurrencyCode))
        {
            throw new InvalidOperationException(
                "Blaaiz API refunds currently support only successful EUR or GBP ClearJunction collections.");
        }

        if (string.IsNullOrWhiteSpace(collection.ProviderCollectionId))
        {
            throw new InvalidOperationException("The collection does not have a provider transaction ID.");
        }

        var completedAt = collection.ConfirmedAt ?? collection.LastUpdatedAt ?? collection.CreatedAt;
        if (completedAt < DateTime.UtcNow.AddDays(-7))
        {
            throw new InvalidOperationException("The provider refund window of seven days has expired.");
        }

        var refundReference = $"KX-RF-{collection.Reference}";
        if (refundReference.Length > 100)
        {
            refundReference = refundReference[..100];
        }

        var result = await _provider.InitiateRefundAsync(
            collection.ProviderCollectionId,
            refundReference,
            Clean(reason),
            collection.TransferId,
            collection.Id,
            ct);

        ApplyProviderRefund(collection, result, reason, changedByUserId, "Admin");
        await UpsertProviderTransactionAsync(collection, result, ct);
        await _db.SaveChangesAsync(ct);

        return ToDto(collection);
    }

    public async Task<RefundDto> RefreshAsync(
        Guid collectionId,
        Guid changedByUserId,
        CancellationToken ct = default)
    {
        var collection = await LoadCollectionAsync(collectionId, ct);

        if (string.IsNullOrWhiteSpace(collection.ProviderRefundId))
        {
            throw new InvalidOperationException("No provider refund has been initiated for this collection.");
        }

        var result = await _provider.GetRefundAsync(
            collection.ProviderRefundId,
            collection.TransferId,
            collection.Id,
            ct);

        ApplyProviderRefund(collection, result, collection.RefundReason, changedByUserId, "Reconciliation");
        await UpsertProviderTransactionAsync(collection, result, ct);
        await _db.SaveChangesAsync(ct);

        return ToDto(collection);
    }

    public async Task<RefundDto> GetAsync(Guid collectionId, CancellationToken ct = default)
    {
        var collection = await _db.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == collectionId && !x.IsDeleted, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("Collection not found.");
        }

        return ToDto(collection);
    }

    private async Task<Collection> LoadCollectionAsync(Guid collectionId, CancellationToken ct)
    {
        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == collectionId && !x.IsDeleted, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("Collection not found.");
        }

        return collection;
    }

    private void ApplyProviderRefund(
        Collection collection,
        RemittanceRefundResult result,
        string? reason,
        Guid changedByUserId,
        string source)
    {
        if (!string.Equals(collection.CurrencyCode, result.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider refund currency mismatch. Expected {collection.CurrencyCode}, received {result.CurrencyCode}.");
        }

        if (Math.Abs(collection.Amount - result.Amount) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Provider refund amount mismatch. Expected {collection.Amount}, received {result.Amount}.");
        }

        collection.ProviderRefundId = result.ProviderRefundId;
        collection.ProviderRefundReference = result.ProviderRefundReference ?? result.ProviderReference;
        collection.RefundReason = Clean(reason);
        collection.RefundFailureReason = Clean(result.FailureReason);
        collection.LastRefundSyncedAt = DateTime.UtcNow;
        collection.LastUpdatedAt = DateTime.UtcNow;
        collection.LastUpdatedByUserId = changedByUserId;

        var status = MapRefundStatus(result.ProviderStatus);
        var metadata = JsonSerializer.Serialize(new
        {
            providerRefundId = result.ProviderRefundId,
            providerStatus = result.ProviderStatus,
            providerRefundReference = result.ProviderRefundReference
        });

        if (collection.Status == status || _collectionStatusService.CanTransition(collection.Status, status))
        {
            _collectionStatusService.ApplyTransition(
                collection,
                status,
                new CollectionStatusTransitionContext(
                    Source: source,
                    Reason: result.FailureReason ?? reason ?? $"Provider refund status is {result.ProviderStatus}.",
                    ChangedByUserId: changedByUserId,
                    ProviderResponseId: result.ProviderRefundId,
                    ResponsePayloadJson: result.RawResponseJson,
                    MetadataJson: metadata,
                    OccurredAt: result.ProviderUpdatedAt ?? result.ProviderCreatedAt));
        }
    }

    private async Task UpsertProviderTransactionAsync(
        Collection collection,
        RemittanceRefundResult result,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _provider.ProviderCode &&
                x.ProviderTransactionId == result.ProviderRefundId,
                ct);

        if (transaction is null)
        {
            transaction = new ProviderTransaction
            {
                ProviderCode = _provider.ProviderCode,
                TransferId = collection.TransferId,
                CollectionId = collection.Id,
                ProviderTransactionId = result.ProviderRefundId,
                TransactionType = "refund"
            };

            _db.ProviderTransactions.Add(transaction);
        }

        transaction.ProviderReference = result.ProviderRefundReference ?? result.ProviderReference;
        transaction.ProviderStatus = result.ProviderStatus;
        transaction.CurrencyCode = result.CurrencyCode;
        transaction.Amount = result.Amount;
        transaction.RawPayloadJson = result.RawResponseJson;
        transaction.ProviderCreatedAt = result.ProviderCreatedAt ?? transaction.ProviderCreatedAt;
        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
    }

    private static CollectionStatus MapRefundStatus(string status) =>
        status.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => CollectionStatus.Refunded,
            "FAILED" or "REJECTED" => CollectionStatus.RefundFailed,
            _ => CollectionStatus.RefundPending
        };

    private static RefundDto ToDto(Collection collection) =>
        new(
            collection.Id,
            collection.TransferId,
            collection.Reference,
            collection.CurrencyCode,
            collection.Amount,
            collection.Status,
            collection.ProviderRefundId,
            collection.ProviderRefundReference,
            collection.RefundReason,
            collection.RefundFailureReason,
            collection.RefundInitiatedAt,
            collection.RefundedAt,
            collection.LastRefundSyncedAt);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
