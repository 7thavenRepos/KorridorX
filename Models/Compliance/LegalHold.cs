using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public sealed class LegalHold : AuditableEntity
{
    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
    public LegalHoldStatus Status { get; set; } = LegalHoldStatus.Active;

    public bool AppliesToAllComplianceData { get; set; }
    public Guid? ComplianceCaseId { get; set; }
    public ComplianceCase? ComplianceCase { get; set; }
    public Guid? RegulatoryReportId { get; set; }
    public RegulatoryReport? RegulatoryReport { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }

    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public string? ReleaseReason { get; set; }
}
