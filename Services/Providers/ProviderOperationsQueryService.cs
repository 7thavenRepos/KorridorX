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
}
