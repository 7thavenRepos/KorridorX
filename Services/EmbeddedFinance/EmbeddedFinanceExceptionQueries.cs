using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

/// <summary>
/// Read-only, database-composable exception feed. No source is truncated before
/// filtering, counting or global pagination. No provider response bodies are selected.
/// </summary>
public static class EmbeddedFinanceExceptionQueries
{
    public const string StaleReason = "Activity has remained unresolved beyond the review threshold.";

    public static IQueryable<EmbeddedFinanceExceptionRow> Build(
        AppDbContext db, Guid? businessProfileId, string? sourceType, string? search, DateTime now)
    {
        var source = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim().ToLowerInvariant();
        IQueryable<EmbeddedFinanceExceptionRow> query = source switch
        {
            "activity" => ActivityExceptions(ActivityCandidates(db, businessProfileId), now),
            "webhookdelivery" => WebhookExceptions(db, businessProfileId),
            "providerprovisioning" => ProvisioningExceptions(db, businessProfileId),
            null => ActivityExceptions(ActivityCandidates(db, businessProfileId), now)
                .Concat(WebhookExceptions(db, businessProfileId))
                .Concat(ProvisioningExceptions(db, businessProfileId)),
            // Preserve the existing empty-result behavior for an unknown source filter.
            _ => WebhookExceptions(db, businessProfileId).Where(x => false)
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search is a literal, case-insensitive substring, not a wildcard expression.
            var pattern = LiteralContainsPattern(search.Trim());
            query = query.Where(x =>
                EF.Functions.ILike(x.Reference!, pattern, "\\") ||
                EF.Functions.ILike(x.BusinessName, pattern, "\\") ||
                EF.Functions.ILike(x.BusinessCustomerName!, pattern, "\\") ||
                EF.Functions.ILike(x.Status, pattern, "\\") ||
                EF.Functions.ILike(x.ProviderCode!, pattern, "\\") ||
                EF.Functions.ILike(x.Reason!, pattern, "\\") ||
                EF.Functions.ILike(x.ActivityType!, pattern, "\\"));
        }
        return query;
    }

    public static string LiteralContainsPattern(string value) =>
        "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public static IOrderedQueryable<EmbeddedFinanceExceptionRow> Order(IQueryable<EmbeddedFinanceExceptionRow> query) =>
        query.OrderByDescending(x => x.SeverityRank)
            .ThenByDescending(x => x.OccurredAt)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.ActivityType)
            .ThenByDescending(x => x.SourceId);

    public static PageMeta Page(int requestedPage, int requestedSize, int total)
    {
        var size = Math.Clamp(requestedSize, 1, 100);
        var last = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        return new PageMeta { Page = Math.Clamp(requestedPage, 1, last), PageSize = size, TotalItems = total };
    }

    public static EmbeddedFinanceAdminExceptionDto ToDto(EmbeddedFinanceExceptionRow row) => new(
        row.SourceType,
        row.SeverityRank == 3 ? "Critical" : row.SeverityRank == 2 ? "High" : "Medium",
        row.SourceId, row.BusinessProfileId, row.BusinessName, row.BusinessCustomerId,
        row.BusinessCustomerName, row.CollectionAccountId, row.Reference, row.Status,
        row.ProviderCode, row.Reason, row.ActivityType, row.ActivityId, row.OccurredAt);

    public static IQueryable<EmbeddedFinanceExceptionRow> ActivityExceptions(
        IQueryable<EmbeddedFinanceExceptionActivityRow> activities, DateTime now)
    {
        var threshold = now.AddMinutes(-30);
        var unresolved = new[] { "Pending", "Initiated", "Processing", "Active", "Submitted" };
        return activities.Select(x => new
            {
                Item = x,
                Stale = x.FinalizedAt == null && x.OccurredAt <= threshold && unresolved.Contains(x.Status)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Item.FailureReason) ||
                x.Item.Status.Contains("Failed") || x.Item.Status == "Rejected" ||
                x.Item.Status == "Expired" || x.Item.Status == "Reversed" || x.Stale)
            .Select(x => new EmbeddedFinanceExceptionRow
            {
                SourceType = "Activity",
                SeverityRank = x.Stale ? 1 : x.Item.Status.Contains("Failed") || x.Item.Status == "Rejected" ? 2 : 1,
                SourceId = x.Item.Id,
                BusinessProfileId = x.Item.BusinessProfileId,
                BusinessName = x.Item.BusinessName,
                BusinessCustomerId = x.Item.BusinessCustomerId,
                BusinessCustomerName = x.Item.BusinessCustomerName,
                CollectionAccountId = x.Item.CollectionAccountId,
                Reference = x.Item.Reference ?? x.Item.ExternalReference,
                Status = x.Item.Status,
                ProviderCode = x.Item.ProviderCode,
                Reason = x.Item.FailureReason ?? (x.Stale ? StaleReason : null),
                ActivityType = x.Item.ActivityType,
                ActivityId = x.Item.Id,
                OccurredAt = x.Item.OccurredAt
            });
    }

    public static IQueryable<EmbeddedFinanceExceptionActivityRow> ActivityCandidates(AppDbContext db, Guid? businessProfileId)
    {
        var collections =
            from item in db.Collections.AsNoTracking()
            join customer in db.BusinessCustomers.AsNoTracking() on item.ContextEntityId equals (Guid?)customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            where !item.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                item.Purpose == PaymentOperationPurpose.AccountFunding && item.ContextEntityType == "BusinessCustomer"
            where !businessProfileId.HasValue || customer.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = "Collection",
                BusinessProfileId = customer.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.Id == item.RelatedEntityId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = item.Reference,
                ExternalReference = null,
                Status = item.Status == CollectionStatus.Pending ? "Pending" :
                item.Status == CollectionStatus.Initiated ? "Initiated" :
                item.Status == CollectionStatus.Processing ? "Processing" :
                item.Status == CollectionStatus.Successful ? "Successful" :
                item.Status == CollectionStatus.Failed ? "Failed" :
                item.Status == CollectionStatus.Cancelled ? "Cancelled" :
                item.Status == CollectionStatus.Refunded ? "Refunded" :
                item.Status == CollectionStatus.RefundPending ? "RefundPending" :
                item.Status == CollectionStatus.Expired ? "Expired" :
                item.Status == CollectionStatus.RefundFailed ? "RefundFailed" :
                ((int)item.Status).ToString(),
                ProviderCode = item.ProviderCode,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.ConfirmedAt,
                FailureReason = null
            };

        var payouts =
            from item in db.Payouts.AsNoTracking()
            join customer in db.BusinessCustomers.AsNoTracking() on item.ContextEntityId equals (Guid?)customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            where !item.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                item.Purpose == PaymentOperationPurpose.Withdrawal && item.ContextEntityType == "BusinessCustomer"
            where !businessProfileId.HasValue || customer.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = "Payout",
                BusinessProfileId = customer.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.FinancialAccountId == item.FinancialAccountId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = item.Reference,
                ExternalReference = null,
                Status = item.Status == PayoutStatus.Pending ? "Pending" :
                item.Status == PayoutStatus.Initiated ? "Initiated" :
                item.Status == PayoutStatus.Processing ? "Processing" :
                item.Status == PayoutStatus.Successful ? "Successful" :
                item.Status == PayoutStatus.Failed ? "Failed" :
                item.Status == PayoutStatus.Reversed ? "Reversed" :
                ((int)item.Status).ToString(),
                ProviderCode = item.ProviderCode,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.CompletedAt ?? item.FailedAt ?? item.ReversedAt,
                FailureReason = item.FailureReason
            };

        var transfers =
            from item in db.Transfers.AsNoTracking()
            join customer in db.BusinessCustomers.AsNoTracking() on item.BusinessCustomerId equals (Guid?)customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            where !item.IsDeleted && !customer.IsDeleted && !business.IsDeleted && item.SourceFinancialAccountId != null
            where !businessProfileId.HasValue || customer.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = "Transfer",
                BusinessProfileId = customer.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.FinancialAccountId == item.SourceFinancialAccountId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = item.Reference,
                ExternalReference = item.ExternalReference,
                Status = item.Status == TransferStatus.Draft ? "Draft" :
                item.Status == TransferStatus.Quoted ? "Quoted" :
                item.Status == TransferStatus.PendingPayment ? "PendingPayment" :
                item.Status == TransferStatus.PaymentReceived ? "PaymentReceived" :
                item.Status == TransferStatus.Processing ? "Processing" :
                item.Status == TransferStatus.PayoutInitiated ? "PayoutInitiated" :
                item.Status == TransferStatus.PayoutCompleted ? "PayoutCompleted" :
                item.Status == TransferStatus.Completed ? "Completed" :
                item.Status == TransferStatus.Failed ? "Failed" :
                item.Status == TransferStatus.Cancelled ? "Cancelled" :
                item.Status == TransferStatus.RefundPending ? "RefundPending" :
                item.Status == TransferStatus.Refunded ? "Refunded" :
                item.Status == TransferStatus.PendingApproval ? "PendingApproval" :
                item.Status == TransferStatus.Rejected ? "Rejected" :
                ((int)item.Status).ToString(),
                ProviderCode = item.ProviderCode,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.CompletedAt ?? item.FailedAt,
                FailureReason = item.FailureReason
            };

        var deposits =
            from item in db.DigitalAssetDepositIntents.AsNoTracking()
            join customer in db.BusinessCustomers.AsNoTracking() on item.BusinessCustomerId equals customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            where !item.IsDeleted && !customer.IsDeleted && !business.IsDeleted
            where !businessProfileId.HasValue || item.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = "DigitalAssetDeposit",
                BusinessProfileId = item.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.FinancialAccountId == item.FinancialAccountId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = item.ProviderCollectionId,
                ExternalReference = null,
                Status = item.Status == CollectionStatus.Pending ? "Pending" :
                item.Status == CollectionStatus.Initiated ? "Initiated" :
                item.Status == CollectionStatus.Processing ? "Processing" :
                item.Status == CollectionStatus.Successful ? "Successful" :
                item.Status == CollectionStatus.Failed ? "Failed" :
                item.Status == CollectionStatus.Cancelled ? "Cancelled" :
                item.Status == CollectionStatus.Refunded ? "Refunded" :
                item.Status == CollectionStatus.RefundPending ? "RefundPending" :
                item.Status == CollectionStatus.Expired ? "Expired" :
                item.Status == CollectionStatus.RefundFailed ? "RefundFailed" :
                ((int)item.Status).ToString(),
                ProviderCode = item.ProviderCode,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.CompletedAt ?? item.FailedAt,
                FailureReason = item.FailureReason
            };

        var withdrawals =
            from item in db.DigitalAssetWithdrawals.AsNoTracking()
            join customer in db.BusinessCustomers.AsNoTracking() on item.BusinessCustomerId equals customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            join payout in db.Payouts.AsNoTracking() on item.PayoutId equals payout.Id
            where !item.IsDeleted && !payout.IsDeleted && !customer.IsDeleted && !business.IsDeleted
            where !businessProfileId.HasValue || item.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = "DigitalAssetWithdrawal",
                BusinessProfileId = item.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.FinancialAccountId == item.FinancialAccountId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = payout.Reference,
                ExternalReference = null,
                Status = item.Status == DigitalAssetWithdrawalStatus.Pending ? "Pending" :
                item.Status == DigitalAssetWithdrawalStatus.Submitted ? "Submitted" :
                item.Status == DigitalAssetWithdrawalStatus.Confirming ? "Confirming" :
                item.Status == DigitalAssetWithdrawalStatus.Completed ? "Completed" :
                item.Status == DigitalAssetWithdrawalStatus.Failed ? "Failed" :
                item.Status == DigitalAssetWithdrawalStatus.Cancelled ? "Cancelled" :
                ((int)item.Status).ToString(),
                ProviderCode = item.ProviderCode,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.CompletedAt ?? item.FailedAt,
                FailureReason = item.FailureReason
            };

        var reservations =
            from item in db.FinancialReservations.AsNoTracking()
            join financialAccount in db.FinancialAccounts.AsNoTracking() on item.FinancialAccountId equals financialAccount.Id
            join customer in db.BusinessCustomers.AsNoTracking() on financialAccount.OwnerId equals customer.Id
            join business in db.BusinessProfiles.AsNoTracking() on customer.BusinessProfileId equals business.Id
            where !item.IsDeleted && !financialAccount.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                financialAccount.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
                (item.Type == FinancialReservationType.MarketplaceTrade || item.Type == FinancialReservationType.InstantTrade)
            where !businessProfileId.HasValue || customer.BusinessProfileId == businessProfileId.Value
            select new EmbeddedFinanceExceptionActivityRow
            {
                Id = item.Id,
                ActivityType = item.Type == FinancialReservationType.MarketplaceTrade ? "MarketplaceTradeReservation" : "InstantTradeReservation",
                BusinessProfileId = customer.BusinessProfileId,
                BusinessName = business.BusinessName,
                BusinessCustomerId = customer.Id,
                BusinessCustomerName = customer.DisplayName,
                CollectionAccountId = db.CollectionAccounts.Where(a => !a.IsDeleted && a.FinancialAccountId == item.FinancialAccountId)
                    .OrderBy(a => a.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
                Reference = item.Reference,
                ExternalReference = null,
                Status = item.Status == FinancialReservationStatus.Active ? "Active" :
                item.Status == FinancialReservationStatus.Captured ? "Captured" :
                item.Status == FinancialReservationStatus.Released ? "Released" :
                ((int)item.Status).ToString(),
                ProviderCode = null,
                OccurredAt = item.CreatedAt,
                FinalizedAt = item.CapturedAt ?? item.ReleasedAt,
                FailureReason = item.ReleaseReason
            };

        return collections.Concat(payouts).Concat(transfers).Concat(deposits).Concat(withdrawals).Concat(reservations);
    }

    public static IQueryable<EmbeddedFinanceExceptionRow> WebhookExceptions(AppDbContext db, Guid? businessProfileId) =>
        from delivery in db.BusinessWebhookDeliveries.AsNoTracking()
        join endpoint in db.BusinessWebhookEndpoints.AsNoTracking() on delivery.BusinessWebhookEndpointId equals endpoint.Id
        join business in db.BusinessProfiles.AsNoTracking() on endpoint.BusinessProfileId equals business.Id
        join webhookEvent in db.BusinessWebhookEvents.AsNoTracking() on delivery.BusinessWebhookEventId equals webhookEvent.Id
        where !delivery.IsDeleted && !endpoint.IsDeleted && !business.IsDeleted &&
            (delivery.Status == BusinessWebhookDeliveryStatus.Retry || delivery.Status == BusinessWebhookDeliveryStatus.DeadLetter) &&
            (!businessProfileId.HasValue || endpoint.BusinessProfileId == businessProfileId.Value)
        select new EmbeddedFinanceExceptionRow
        {
            SourceType = "WebhookDelivery",
            SeverityRank = delivery.Status == BusinessWebhookDeliveryStatus.DeadLetter ? 3 : 2,
            SourceId = delivery.Id,
            BusinessProfileId = endpoint.BusinessProfileId,
            BusinessName = business.BusinessName,
            BusinessCustomerId = null,
            BusinessCustomerName = null,
            CollectionAccountId = null,
            Reference = webhookEvent.EventId,
            Status = delivery.Status == BusinessWebhookDeliveryStatus.DeadLetter ? "DeadLetter" : "Retry",
            ProviderCode = null,
            Reason = delivery.ErrorMessage,
            ActivityType = null,
            ActivityId = null,
            OccurredAt = delivery.DeadLetteredAt ?? delivery.LastAttemptAt ?? delivery.CreatedAt
        };

    public static IQueryable<EmbeddedFinanceExceptionRow> ProvisioningExceptions(AppDbContext db, Guid? businessProfileId) =>
        from mapping in db.ProviderAccountMappings.AsNoTracking()
        join account in db.CollectionAccounts.AsNoTracking() on mapping.CollectionAccountId equals account.Id
        join customer in db.BusinessCustomers.AsNoTracking() on account.BusinessCustomerId equals customer.Id
        join business in db.BusinessProfiles.AsNoTracking() on account.BusinessProfileId equals business.Id
        where !mapping.IsDeleted && !account.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
            mapping.Status == ProviderAccountMappingStatus.Failed &&
            (!businessProfileId.HasValue || account.BusinessProfileId == businessProfileId.Value)
        select new EmbeddedFinanceExceptionRow
        {
            SourceType = "ProviderProvisioning",
            SeverityRank = 2,
            SourceId = mapping.Id,
            BusinessProfileId = account.BusinessProfileId,
            BusinessName = business.BusinessName,
            BusinessCustomerId = account.BusinessCustomerId,
            BusinessCustomerName = customer.DisplayName,
            CollectionAccountId = account.Id,
            Reference = account.ExternalReference,
            Status = "Failed",
            ProviderCode = mapping.ProviderCode,
            Reason = mapping.FailureReason,
            ActivityType = null,
            ActivityId = null,
            OccurredAt = mapping.LastUpdatedAt ?? mapping.CreatedAt
        };
}

// Composable projections only: these are not entities and add no tables/migrations.
public sealed class EmbeddedFinanceExceptionRow
{
    public string SourceType { get; init; } = "";
    public int SeverityRank { get; init; }
    public Guid SourceId { get; init; }
    public Guid BusinessProfileId { get; init; }
    public string BusinessName { get; init; } = "";
    public Guid? BusinessCustomerId { get; init; }
    public string? BusinessCustomerName { get; init; }
    public Guid? CollectionAccountId { get; init; }
    public string? Reference { get; init; }
    public string Status { get; init; } = "";
    public string? ProviderCode { get; init; }
    public string? Reason { get; init; }
    public string? ActivityType { get; init; }
    public Guid? ActivityId { get; init; }
    public DateTime OccurredAt { get; init; }
}

public sealed class EmbeddedFinanceExceptionActivityRow
{
    public Guid Id { get; init; }
    public string ActivityType { get; init; } = "";
    public Guid BusinessProfileId { get; init; }
    public string BusinessName { get; init; } = "";
    public Guid BusinessCustomerId { get; init; }
    public string BusinessCustomerName { get; init; } = "";
    public Guid? CollectionAccountId { get; init; }
    public string? Reference { get; init; }
    public string? ExternalReference { get; init; }
    public string Status { get; init; } = "";
    public string? ProviderCode { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public string? FailureReason { get; init; }
}
