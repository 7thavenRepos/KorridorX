using KorridorX.Data;
using KorridorX.Dtos.Providers;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Providers;

public class ProviderOperationsQueryService : IProviderOperationsQueryService
{
    private readonly AppDbContext _db;

    public ProviderOperationsQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProviderTransactionDto>> GetTransactionsAsync(
        string? transactionType,
        string? providerStatus,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ProviderTransactions
            .AsNoTracking()
            .Where(x => x.ProviderCode == ProviderCode.Blaaiz && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var type = transactionType.Trim().ToLower();
            query = query.Where(x => x.TransactionType.ToLower() == type);
        }

        if (!string.IsNullOrWhiteSpace(providerStatus))
        {
            var status = providerStatus.Trim().ToLower();
            query = query.Where(x => x.ProviderStatus.ToLower() == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.ProviderTransactionId.ToLower().Contains(value) ||
                (x.ProviderReference != null && x.ProviderReference.ToLower().Contains(value)) ||
                x.CurrencyCode.ToLower().Contains(value));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProviderTransactionDto(
                x.Id,
                x.ProviderCode,
                x.TransferId,
                x.CollectionId,
                x.PayoutId,
                x.ProviderTransactionId,
                x.ProviderReference,
                x.TransactionType,
                x.ProviderStatus,
                x.CurrencyCode,
                x.Amount,
                x.AmountWithoutFee,
                x.ProviderFeeAmount,
                x.ProviderFeeCurrencyCode,
                x.ProviderCreatedAt,
                x.LastSyncedAt,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<ProviderRequestLogDto>> GetRequestLogsAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ProviderRequestLogs
            .AsNoTracking()
            .Where(x => x.ProviderCode == ProviderCode.Blaaiz && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ProviderRequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.Endpoint.ToLower().Contains(value) ||
                (x.ErrorMessage != null && x.ErrorMessage.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new ProviderRequestLogDto(
                x.Id,
                x.ProviderCode,
                x.Endpoint,
                x.HttpMethod,
                x.Status,
                x.ResponseStatusCode,
                x.RelatedTransferId,
                x.RelatedCollectionId,
                x.RelatedPayoutId,
                x.ErrorMessage,
                x.DurationMs,
                x.RequestedAt,
                x.RespondedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<ProviderRequestLogDetailsDto> GetRequestLogAsync(
        Guid requestLogId,
        CancellationToken ct = default)
    {
        var request = await _db.ProviderRequestLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == requestLogId &&
                x.ProviderCode == ProviderCode.Blaaiz &&
                !x.IsDeleted,
                ct);

        if (request is null)
        {
            throw new InvalidOperationException("Provider request log not found.");
        }

        var summary = new ProviderRequestLogDto(
            request.Id,
            request.ProviderCode,
            request.Endpoint,
            request.HttpMethod,
            request.Status,
            request.ResponseStatusCode,
            request.RelatedTransferId,
            request.RelatedCollectionId,
            request.RelatedPayoutId,
            request.ErrorMessage,
            request.DurationMs,
            request.RequestedAt,
            request.RespondedAt);

        return new ProviderRequestLogDetailsDto(
            summary,
            request.RequestHeadersJson,
            request.RequestBodyJson,
            request.ResponseBodyJson);
    }

    public async Task<PagedResult<ProviderWebhookEventDto>> GetWebhookEventsAsync(
        string? processingStatus,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.WebhookEvents
            .AsNoTracking()
            .Where(x => x.ProviderCode == ProviderCode.Blaaiz && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(processingStatus) &&
            Enum.TryParse<WebhookProcessingStatus>(processingStatus, true, out var parsedStatus))
        {
            query = query.Where(x => x.ProcessingStatus == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = eventType.Trim().ToLower();
            query = query.Where(x => x.EventType.ToLower().Contains(type));
        }

        return await query
            .OrderByDescending(x => x.ReceivedAt)
            .Select(x => new ProviderWebhookEventDto(
                x.Id,
                x.ProviderCode,
                x.ProviderEventId,
                x.EventType,
                x.ProcessingStatus,
                x.IsDuplicate,
                x.ReceivedAt,
                x.ProcessedAt,
                x.ErrorMessage,
                x.Attempts.Count))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<ProviderWebhookEventDetailsDto> GetWebhookEventAsync(
        Guid webhookEventId,
        CancellationToken ct = default)
    {
        var webhook = await _db.WebhookEvents
            .AsNoTracking()
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.Id == webhookEventId &&
                x.ProviderCode == ProviderCode.Blaaiz &&
                !x.IsDeleted,
                ct);

        if (webhook is null)
        {
            throw new InvalidOperationException("Provider webhook event not found.");
        }

        var summary = new ProviderWebhookEventDto(
            webhook.Id,
            webhook.ProviderCode,
            webhook.ProviderEventId,
            webhook.EventType,
            webhook.ProcessingStatus,
            webhook.IsDuplicate,
            webhook.ReceivedAt,
            webhook.ProcessedAt,
            webhook.ErrorMessage,
            webhook.Attempts.Count);

        var attempts = webhook.Attempts
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new ProviderWebhookProcessingAttemptDto(
                x.Id,
                x.Status,
                x.ErrorMessage,
                x.StartedAt,
                x.FinishedAt))
            .ToArray();

        return new ProviderWebhookEventDetailsDto(
            summary,
            webhook.RawPayloadJson,
            attempts);
    }

    public async Task<PagedResult<FailedPayoutRecoveryDto>> GetFailedPayoutsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Payouts
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.Status == PayoutStatus.Failed);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();

            query = query.Where(x =>
                x.Reference.ToLower().Contains(value) ||
                (x.ProviderPayoutId != null && x.ProviderPayoutId.ToLower().Contains(value)) ||
                (x.ProviderReference != null && x.ProviderReference.ToLower().Contains(value)) ||
                (x.FailureReason != null && x.FailureReason.ToLower().Contains(value)) ||
                (x.Transfer != null && x.Transfer.Reference.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.FailedAt ?? x.CreatedAt)
            .Select(x => new FailedPayoutRecoveryDto(
                x.Id,
                x.TransferId,
                x.Transfer == null ? null : x.Transfer.Reference,
                x.Reference,
                x.CurrencyCode,
                x.Amount,
                x.Status,
                x.ProviderCode,
                x.ProviderPayoutId,
                x.ProviderReference,
                x.FailureReason,
                x.FailedAt,
                x.Attempts.Count,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<PendingPayoutDispatchDto>> GetPendingPayoutsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Transfers
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == TransferStatus.PaymentReceived || x.Status == TransferStatus.Processing) &&
                !_db.Payouts.Any(p => p.TransferId == x.Id && !p.IsDeleted));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.Reference.ToLower().Contains(value) ||
                (x.ExternalReference != null && x.ExternalReference.ToLower().Contains(value)) ||
                x.SourceCurrencyCode.ToLower().Contains(value) ||
                x.DestinationCurrencyCode.ToLower().Contains(value));
        }

        return await query
            .OrderBy(x => x.PaymentReceivedAt ?? x.CreatedAt)
            .Select(x => new PendingPayoutDispatchDto(
                x.Id,
                x.Reference,
                x.BusinessProfileId != null ? "Business" : "Consumer",
                x.Status,
                x.SourceCurrencyCode,
                x.SourceAmount,
                x.DestinationCurrencyCode,
                x.DestinationAmount,
                x.IsComplianceHold,
                x.ComplianceHoldReason,
                x.IsOperationalHold,
                x.OperationalHoldReason,
                !x.IsComplianceHold && !x.IsOperationalHold,
                x.PaymentReceivedAt,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<RefundOperationDto>> GetRefundOperationsAsync(
        string? search,
        CollectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Collections
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Purpose == PaymentOperationPurpose.Remittance &&
                (x.Status == CollectionStatus.Successful ||
                 x.Status == CollectionStatus.RefundPending ||
                 x.Status == CollectionStatus.RefundFailed ||
                 x.Status == CollectionStatus.Refunded));

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.Reference.ToLower().Contains(value) ||
                (x.ProviderCollectionId != null && x.ProviderCollectionId.ToLower().Contains(value)) ||
                (x.ProviderRefundId != null && x.ProviderRefundId.ToLower().Contains(value)) ||
                (x.Transfer != null && x.Transfer.Reference.ToLower().Contains(value)) ||
                x.CurrencyCode.ToLower().Contains(value));
        }

        var refundWindowStart = DateTime.UtcNow.AddDays(-7);

        return await query
            .OrderByDescending(x => x.RefundInitiatedAt ?? x.ConfirmedAt ?? x.CreatedAt)
            .Select(x => new RefundOperationDto(
                x.Id,
                x.TransferId,
                x.Transfer == null ? null : x.Transfer.Reference,
                x.Reference,
                x.CurrencyCode,
                x.Amount,
                x.Status,
                x.ProviderCode,
                x.ProviderCollectionId,
                x.ProviderReference,
                x.ProviderRefundId,
                x.ProviderRefundReference,
                x.RefundReason,
                x.RefundFailureReason,
                x.Status == CollectionStatus.Successful &&
                    (x.CurrencyCode == "EUR" || x.CurrencyCode == "GBP") &&
                    x.ProviderCollectionId != null &&
                    (x.ConfirmedAt ?? x.LastUpdatedAt ?? x.CreatedAt) >= refundWindowStart,
                x.ProviderRefundId != null &&
                    (x.Status == CollectionStatus.RefundPending || x.Status == CollectionStatus.RefundFailed),
                x.Status != CollectionStatus.Successful
                    ? null
                    : x.CurrencyCode != "EUR" && x.CurrencyCode != "GBP"
                        ? "Provider refunds support only EUR and GBP collections."
                        : x.ProviderCollectionId == null
                            ? "The collection has no provider transaction ID."
                            : (x.ConfirmedAt ?? x.LastUpdatedAt ?? x.CreatedAt) < refundWindowStart
                                ? "The seven-day provider refund window has expired."
                                : null,
                x.ConfirmedAt,
                x.RefundInitiatedAt,
                x.RefundedAt,
                x.LastRefundSyncedAt,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }
}
