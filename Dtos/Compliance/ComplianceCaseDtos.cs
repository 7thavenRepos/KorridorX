using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public sealed record ScreeningMatchDto(
    Guid Id,
    WatchlistType WatchlistType,
    string ListName,
    string MatchedName,
    string? ProviderMatchId,
    decimal MatchScore,
    string MatchReason,
    string? CountryCode,
    DateTime? DateOfBirth,
    bool IsFalsePositive);

public sealed record ScreeningRecordDto(
    Guid Id,
    ScreeningSubjectType SubjectType,
    ScreeningReason Reason,
    ScreeningStatus Status,
    Guid? CustomerProfileId,
    Guid? BusinessProfileId,
    Guid? RecipientId,
    Guid? BusinessBeneficiaryId,
    Guid? BusinessBeneficialOwnerId,
    Guid? TransferId,
    string SubjectName,
    string? CountryCode,
    string ProviderCode,
    string? ProviderReference,
    decimal HighestMatchScore,
    bool IsBlocking,
    DateTime ScreenedAt,
    DateTime? ExpiresAt,
    string? ErrorMessage,
    IReadOnlyList<ScreeningMatchDto> Matches);

public sealed record ComplianceCaseDto(
    Guid Id,
    string Reference,
    ComplianceCaseType CaseType,
    ComplianceCaseStatus Status,
    ComplianceCasePriority Priority,
    string Title,
    bool IsBlocking,
    Guid? CustomerProfileId,
    Guid? BusinessProfileId,
    Guid? RecipientId,
    Guid? BusinessBeneficiaryId,
    Guid? BusinessBeneficialOwnerId,
    Guid? TransferId,
    string? TransferReference,
    Guid? ScreeningRecordId,
    Guid? AmlFlagId,
    Guid? AssignedToUserId,
    DateTime OpenedAt,
    DateTime? DueAt,
    ComplianceCaseDecision? Decision,
    DateTime? DecidedAt,
    DateTime? ResolvedAt);

public sealed record ComplianceCaseNoteDto(
    Guid Id,
    string Note,
    bool IsInternal,
    Guid CreatedByUserId,
    DateTime CreatedAt);

public sealed record ComplianceCaseEvidenceDto(
    Guid Id,
    string EvidenceType,
    string Title,
    string? Description,
    string? Source,
    string? ExternalReference,
    string? StorageKey,
    string? MetadataJson,
    Guid AddedByUserId,
    DateTime CreatedAt);

public sealed record ComplianceCaseDetailsDto(
    ComplianceCaseDto Case,
    string Description,
    string? DecisionReason,
    Guid? DecidedByUserId,
    ScreeningRecordDto? Screening,
    AmlFlagDto? AmlFlag,
    IReadOnlyList<ComplianceCaseNoteDto> Notes,
    IReadOnlyList<ComplianceCaseEvidenceDto> Evidence);

public sealed class AssignComplianceCaseRequestDto
{
    public Guid? AssignedToUserId { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }
}

public sealed class AddComplianceCaseNoteRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Note { get; set; } = string.Empty;

    public bool IsInternal { get; set; } = true;
}


public sealed class AddComplianceCaseEvidenceRequestDto
{
    [Required]
    [MaxLength(100)]
    public string EvidenceType { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? ExternalReference { get; set; }

    [MaxLength(1000)]
    public string? StorageKey { get; set; }

    public string? MetadataJson { get; set; }
}

public sealed class DecideComplianceCaseRequestDto
{
    [Required]
    public ComplianceCaseDecision Decision { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record TransactionMonitoringDecisionDto(
    Guid TransferId,
    int Score,
    RiskLevel RiskLevel,
    RiskDecision Decision,
    bool IsComplianceHold,
    IReadOnlyList<string> TriggeredRules,
    DateTime MonitoredAt);

public sealed record ComplianceCaseSummaryDto(
    int Open,
    int InReview,
    int Escalated,
    int Blocking,
    int Critical,
    int ResolvedLast30Days,
    int PendingScreeningMatches);

public sealed class ManualScreeningRequestDto
{
    [Required]
    public ScreeningSubjectType SubjectType { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    public Guid? TransferId { get; set; }
}
