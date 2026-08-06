namespace KorridorX.Dtos.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string Category,
    string EntityName,
    string? EntityId,
    string? OldValuesJson,
    string? NewValuesJson,
    string? MetadataJson,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    DateTime OccurredAt);

public sealed record AuditRecordRequest(
    string Action,
    string Category,
    string EntityName,
    string? EntityId = null,
    object? OldValues = null,
    object? NewValues = null,
    object? Metadata = null,
    Guid? UserId = null);
