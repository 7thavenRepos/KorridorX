using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Customers;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Pagination;

/// <summary>SQL-composable read queries. No provider calls, writes, or unbounded materialization.</summary>
public static class WebHistoryQueries
{
    public sealed class ActivityKey
    {
        public Guid Id { get; set; }
        public int Kind { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public static IQueryable<Collection> DigitalCollections(AppDbContext db, Guid businessId) =>
        db.Collections.AsNoTracking().Where(x =>
            x.ContextEntityType == nameof(BusinessProfile) &&
            x.ContextEntityId == businessId &&
            x.PaymentMethod == PaymentMethod.DigitalAsset && !x.IsDeleted);

    public static IQueryable<Payout> DigitalPayouts(AppDbContext db, Guid businessId) =>
        db.Payouts.AsNoTracking().Where(x =>
            x.ContextEntityType == nameof(BusinessProfile) &&
            x.ContextEntityId == businessId &&
            x.PaymentMethod == PaymentMethod.DigitalAsset && !x.IsDeleted);

    public static IQueryable<DigitalAssetDepositIntent> PendingDigitalIntents(AppDbContext db, Guid businessId) =>
        db.DigitalAssetDepositIntents.AsNoTracking().Where(x =>
            x.BusinessProfileId == businessId && x.BusinessCustomerId == null &&
            x.CollectionId == null && !x.IsDeleted);

    public static IOrderedQueryable<ActivityKey> DigitalActivityKeys(AppDbContext db, Guid businessId)
    {
        // UNION ALL contains only scalars. Enum display strings are mapped AFTER paging,
        // avoiding a client-side projection before a relational set operation.
        var deposits = DigitalCollections(db, businessId)
            .Select(x => new ActivityKey { Id = x.Id, Kind = 1, OccurredAt = x.CreatedAt });
        var withdrawals = DigitalPayouts(db, businessId)
            .Select(x => new ActivityKey { Id = x.Id, Kind = 2, OccurredAt = x.CreatedAt });
        var intents = PendingDigitalIntents(db, businessId)
            .Select(x => new ActivityKey { Id = x.Id, Kind = 3, OccurredAt = x.CreatedAt });
        return deposits.Concat(withdrawals).Concat(intents)
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Kind).ThenBy(x => x.Id);
    }

    public static IQueryable<BusinessWebhookDeliveryDto> WebhookDeliveries(
        AppDbContext db, Guid businessId, BusinessWebhookDeliveryStatus? status)
    {
        var query = db.BusinessWebhookDeliveries.AsNoTracking().Where(x =>
            x.BusinessWebhookEndpoint.BusinessProfileId == businessId &&
            x.BusinessWebhookEvent.BusinessProfileId == businessId &&
            !x.IsDeleted && !x.BusinessWebhookEndpoint.IsDeleted && !x.BusinessWebhookEvent.IsDeleted);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new BusinessWebhookDeliveryDto(x.Id, x.BusinessWebhookEndpointId,
                x.BusinessWebhookEventId, x.BusinessWebhookEvent.EventId, x.BusinessWebhookEvent.EventType,
                x.Status, x.AttemptCount, x.NextAttemptAt, x.LastAttemptAt, x.DeliveredAt,
                x.DeadLetteredAt, x.LastResponseStatusCode, x.ErrorMessage, x.CreatedAt));
    }

    public static IQueryable<DigitalAssetNetworkTransactionDto> ReconciliationExceptions(AppDbContext db) =>
        db.DigitalAssetNetworkTransactions.AsNoTracking()
            .Where(x => !x.IsDeleted && (
                x.Status == DigitalAssetTransactionStatus.Failed ||
                (x.Direction == DigitalAssetTransactionDirection.Inbound &&
                 x.Status == DigitalAssetTransactionStatus.Confirmed &&
                 (!x.CollectionId.HasValue || !x.LedgerTransactionId.HasValue)) ||
                (x.Direction == DigitalAssetTransactionDirection.Outbound &&
                 x.Status == DigitalAssetTransactionStatus.Confirmed && !x.PayoutId.HasValue)))
            .OrderByDescending(x => x.LastUpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new DigitalAssetNetworkTransactionDto
            {
                Id = x.Id, ProviderCode = x.ProviderCode, ProviderTransactionId = x.ProviderTransactionId,
                ProviderReference = x.ProviderReference, AssetNetworkId = x.AssetNetworkId,
                AssetCode = x.AssetCode, NetworkCode = x.AssetNetwork.NetworkCode,
                Direction = x.Direction, Status = x.Status, TransactionHash = x.TransactionHash,
                FromAddress = x.FromAddress, ToAddress = x.ToAddress, DestinationTag = x.DestinationTag,
                Amount = x.Amount, NetworkFee = x.NetworkFee, Confirmations = x.Confirmations,
                RequiredConfirmations = x.RequiredConfirmations, BlockNumber = x.BlockNumber,
                CollectionId = x.CollectionId, PayoutId = x.PayoutId, LedgerTransactionId = x.LedgerTransactionId,
                ObservedAt = x.ObservedAt, ConfirmedAt = x.ConfirmedAt, FailedAt = x.FailedAt,
                CreatedAt = x.CreatedAt, LastUpdatedAt = x.LastUpdatedAt
            });

    public static IQueryable<DigitalAssetFeeExceptionDto> FeeExceptions(AppDbContext db, decimal minimumAbsoluteVariance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumAbsoluteVariance);
        return db.DigitalAssetWithdrawals.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ActualNetworkFee.HasValue && x.NetworkFeeVariance.HasValue &&
                (x.NetworkFeeVariance.Value >= minimumAbsoluteVariance ||
                 x.NetworkFeeVariance.Value <= -minimumAbsoluteVariance))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new DigitalAssetFeeExceptionDto
            {
                WithdrawalId = x.Id, AssetCode = x.AssetCode, ProviderCode = x.ProviderCode,
                QuotedNetworkFee = x.NetworkFee, ActualNetworkFee = x.ActualNetworkFee!.Value,
                Variance = x.NetworkFeeVariance!.Value, CreatedAt = x.CreatedAt
            });
    }
}
