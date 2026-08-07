using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public class CreateRegulatoryReportRequestDto
{
    [Required]
    public RegulatoryReportType ReportType { get; set; }

    [Required, MaxLength(10)]
    public string JurisdictionCode { get; set; } = "";

    [MaxLength(200)]
    public string? RegulatoryAuthority { get; set; }

    [Required, MaxLength(12000)]
    public string Narrative { get; set; } = "";

    [Required, MaxLength(4000)]
    public string SuspicionReason { get; set; } = "";

    public DateTime? ActivityStartedAt { get; set; }
    public DateTime? ActivityEndedAt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TotalAmount { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public DateTime? FilingDueAt { get; set; }
}

public sealed class UpdateRegulatoryReportRequestDto : CreateRegulatoryReportRequestDto
{
}

public sealed class ReviewRegulatoryReportRequestDto
{
    [Required]
    public RegulatoryReportApprovalDecision Decision { get; set; }

    [Required, MaxLength(4000)]
    public string Reason { get; set; } = "";
}

public sealed class MarkRegulatoryReportFiledRequestDto
{
    [Required, MaxLength(200)]
    public string FilingReference { get; set; } = "";

    public DateTime? FiledAt { get; set; }
}

public sealed record RegulatoryReportDto
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = "";
    public RegulatoryReportType ReportType { get; init; }
    public RegulatoryReportStatus Status { get; init; }
    public Guid ComplianceCaseId { get; init; }
    public string ComplianceCaseReference { get; init; } = "";
    public string JurisdictionCode { get; init; } = "";
    public string? RegulatoryAuthority { get; init; }
    public string? FilingReference { get; init; }
    public string Narrative { get; init; } = "";
    public string SuspicionReason { get; init; } = "";
    public DateTime? ActivityStartedAt { get; init; }
    public DateTime? ActivityEndedAt { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? CurrencyCode { get; init; }
    public Guid PreparedByUserId { get; init; }
    public DateTime? SubmittedForApprovalAt { get; init; }
    public Guid? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public Guid? FiledByUserId { get; init; }
    public DateTime? FiledAt { get; init; }
    public DateTime? FilingDueAt { get; init; }
    public DateTime? LastExportedAt { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
}

public sealed record RegulatoryReportSummaryDto(
    int Draft,
    int PendingApproval,
    int Approved,
    int Filed,
    int Rejected,
    int Overdue,
    int FiledLast30Days);

public sealed record RegulatoryReportExportResult(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed class UpsertRetentionPolicyRequestDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required]
    public RetentionRecordType RecordType { get; set; }

    [Range(30, 36500)]
    public int RetentionDays { get; set; }

    [Required]
    public RetentionAction Action { get; set; } = RetentionAction.ReviewOnly;

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Description { get; set; }
}

public sealed record RetentionPolicyDto(
    Guid Id,
    string Name,
    RetentionRecordType RecordType,
    int RetentionDays,
    RetentionAction Action,
    bool IsActive,
    string? Description,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed class CreateLegalHoldRequestDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, MaxLength(4000)]
    public string Reason { get; set; } = "";

    public bool AppliesToAllComplianceData { get; set; }
    public Guid? ComplianceCaseId { get; set; }
    public Guid? RegulatoryReportId { get; set; }

    [MaxLength(150)]
    public string? EntityName { get; set; }

    [MaxLength(100)]
    public string? EntityId { get; set; }
}

public sealed class ReleaseLegalHoldRequestDto
{
    [Required, MaxLength(4000)]
    public string Reason { get; set; } = "";
}

public sealed record LegalHoldDto(
    Guid Id,
    string Reference,
    string Name,
    string Reason,
    LegalHoldStatus Status,
    bool AppliesToAllComplianceData,
    Guid? ComplianceCaseId,
    Guid? RegulatoryReportId,
    string? EntityName,
    string? EntityId,
    DateTime EffectiveAt,
    DateTime? ReleasedAt,
    Guid? ReleasedByUserId,
    string? ReleaseReason,
    DateTime CreatedAt);

public sealed record RetentionExecutionDto(
    Guid Id,
    Guid DataRetentionPolicyId,
    string PolicyName,
    bool IsDryRun,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int CandidateCount,
    int ProcessedCount,
    int SkippedLegalHoldCount,
    string? ErrorMessage);

public sealed record RegulatoryComplianceManagementSummaryDto(
    RegulatoryReportSummaryDto Reports,
    int ActiveLegalHolds,
    int ActiveRetentionPolicies,
    int OpenComplianceCases,
    int BlockingComplianceCases,
    int OverdueComplianceCases,
    int ExpiringScreeningsNext7Days);
