using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public sealed class ComplianceCaseEvidence : BaseEntity
{
    public Guid ComplianceCaseId { get; set; }
    public ComplianceCase ComplianceCase { get; set; } = null!;

    public string EvidenceType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? ExternalReference { get; set; }
    public string? StorageKey { get; set; }
    public string? MetadataJson { get; set; }
    public Guid AddedByUserId { get; set; }
}
