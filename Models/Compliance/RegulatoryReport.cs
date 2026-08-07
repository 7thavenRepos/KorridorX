using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public sealed class RegulatoryReport : AuditableEntity
{
    public string Reference { get; set; } = "";
    public RegulatoryReportType ReportType { get; set; }
    public RegulatoryReportStatus Status { get; set; } = RegulatoryReportStatus.Draft;

    public Guid ComplianceCaseId { get; set; }
    public ComplianceCase ComplianceCase { get; set; } = null!;

    public string JurisdictionCode { get; set; } = "";
    public string? RegulatoryAuthority { get; set; }
    public string? FilingReference { get; set; }

    public string Narrative { get; set; } = "";
    public string SuspicionReason { get; set; } = "";
    public DateTime? ActivityStartedAt { get; set; }
    public DateTime? ActivityEndedAt { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? CurrencyCode { get; set; }

    public Guid PreparedByUserId { get; set; }
    public DateTime? SubmittedForApprovalAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? FiledByUserId { get; set; }
    public DateTime? FiledAt { get; set; }
    public DateTime? FilingDueAt { get; set; }
    public DateTime? LastExportedAt { get; set; }
    public int Version { get; set; } = 1;
}
