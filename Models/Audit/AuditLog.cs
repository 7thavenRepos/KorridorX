using KorridorX.Models.Common;

namespace KorridorX.Models.Audit;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public string Action { get; set; } = "";
    public string Category { get; set; } = "General";
    public string EntityName { get; set; } = "";
    public string? EntityId { get; set; }

    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}