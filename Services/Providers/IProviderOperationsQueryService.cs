using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Providers;

public interface IProviderOperationsQueryService
{
    Task<PagedResult<ProviderTransactionDto>> GetTransactionsAsync(
        string? transactionType,
        string? providerStatus,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<ProviderRequestLogDto>> GetRequestLogsAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ProviderRequestLogDetailsDto> GetRequestLogAsync(
        Guid requestLogId,
        CancellationToken ct = default);

    Task<PagedResult<ProviderWebhookEventDto>> GetWebhookEventsAsync(
        string? processingStatus,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ProviderWebhookEventDetailsDto> GetWebhookEventAsync(
        Guid webhookEventId,
        CancellationToken ct = default);

    Task<PagedResult<FailedPayoutRecoveryDto>> GetFailedPayoutsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<PendingPayoutDispatchDto>> GetPendingPayoutsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<RefundOperationDto>> GetRefundOperationsAsync(
        string? search,
        CollectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
