using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public sealed record OutboundFundsRestrictionDto(
    Guid Id,
    OutboundFundsRestrictionSubjectType SubjectType,
    Guid SubjectId,
    string SubjectDisplayName,
    OutboundFundsRestrictionSource Source,
    string InternalReason,
    string? ExternalReference,
    bool IsActive,
    Guid AppliedByUserId,
    DateTime AppliedAt,
    Guid? LiftedByUserId,
    DateTime? LiftedAt,
    string? LiftReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed class ApplyOutboundFundsRestrictionRequestDto
{
    public OutboundFundsRestrictionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public OutboundFundsRestrictionSource Source { get; set; }

    [Required, MaxLength(2000)]
    public string InternalReason { get; set; } = "";

    [MaxLength(300)]
    public string? ExternalReference { get; set; }
}

public sealed class LiftOutboundFundsRestrictionRequestDto
{
    [Required, MaxLength(2000)]
    public string Reason { get; set; } = "";
}


public sealed record OutboundFundsRestrictionSubjectLookupDto(
    OutboundFundsRestrictionSubjectType SubjectType,
    Guid SubjectId,
    string DisplayName,
    string? Reference,
    string? CountryCode,
    bool HasActiveRestriction);
