using KorridorX.Dtos.Audit;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Audit;

public interface IAuditService
{
    void Stage(AuditRecordRequest request);

    Task RecordAsync(
        AuditRecordRequest request,
        CancellationToken ct = default);

    Task<PagedResult<AuditLogDto>> GetAsync(
        string? category,
        string? action,
        string? entityName,
        string? entityId,
        Guid? userId,
        string? correlationId,
        string? search,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AuditLogDto> GetByIdAsync(
        Guid auditLogId,
        CancellationToken ct = default);
}