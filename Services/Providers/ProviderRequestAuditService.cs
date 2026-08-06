using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Providers;

public class ProviderRequestAuditService : IProviderRequestAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProviderRequestAuditService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Guid> StartAsync(
        ProviderCode providerCode,
        string endpoint,
        string httpMethod,
        string? requestHeadersJson,
        string? requestBodyJson,
        Guid? relatedTransferId = null,
        Guid? relatedCollectionId = null,
        Guid? relatedPayoutId = null,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = new ProviderRequestLog
        {
            ProviderCode = providerCode,
            Endpoint = endpoint,
            HttpMethod = httpMethod,
            RequestHeadersJson = requestHeadersJson,
            RequestBodyJson = requestBodyJson,
            Status = ProviderRequestStatus.Pending,
            RelatedTransferId = relatedTransferId,
            RelatedCollectionId = relatedCollectionId,
            RelatedPayoutId = relatedPayoutId,
            RequestedAt = DateTime.UtcNow
        };

        db.ProviderRequestLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return log.Id;
    }

    public async Task CompleteAsync(
        Guid requestLogId,
        int responseStatusCode,
        string? responseBodyJson,
        long durationMs,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = await db.ProviderRequestLogs.FirstOrDefaultAsync(x => x.Id == requestLogId, ct);
        if (log is null)
        {
            return;
        }

        log.Status = ProviderRequestStatus.Successful;
        log.ResponseStatusCode = responseStatusCode;
        log.ResponseBodyJson = responseBodyJson;
        log.RespondedAt = DateTime.UtcNow;
        log.DurationMs = durationMs;
        log.LastUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task FailAsync(
        Guid requestLogId,
        int? responseStatusCode,
        string? responseBodyJson,
        string errorMessage,
        long durationMs,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = await db.ProviderRequestLogs.FirstOrDefaultAsync(x => x.Id == requestLogId, ct);
        if (log is null)
        {
            return;
        }

        log.Status = ProviderRequestStatus.Failed;
        log.ResponseStatusCode = responseStatusCode;
        log.ResponseBodyJson = responseBodyJson;
        log.ErrorMessage = errorMessage.Length <= 2000
            ? errorMessage
            : errorMessage[..2000];
        log.RespondedAt = DateTime.UtcNow;
        log.DurationMs = durationMs;
        log.LastUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
