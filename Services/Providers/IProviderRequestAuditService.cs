using KorridorX.Models.Enums;

namespace KorridorX.Services.Providers;

public interface IProviderRequestAuditService
{
    Task<Guid> StartAsync(
        ProviderCode providerCode,
        string endpoint,
        string httpMethod,
        string? requestHeadersJson,
        string? requestBodyJson,
        Guid? relatedTransferId = null,
        Guid? relatedCollectionId = null,
        Guid? relatedPayoutId = null,
        CancellationToken ct = default);

    Task CompleteAsync(
        Guid requestLogId,
        int responseStatusCode,
        string? responseBodyJson,
        long durationMs,
        CancellationToken ct = default);

    Task FailAsync(
        Guid requestLogId,
        int? responseStatusCode,
        string? responseBodyJson,
        string errorMessage,
        long durationMs,
        CancellationToken ct = default);
}
