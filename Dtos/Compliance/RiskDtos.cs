using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public record TransferRiskDecisionDto(
    Guid TransferId,
    int Score,
    RiskLevel Level,
    RiskDecision Decision,
    bool IsComplianceHold,
    IReadOnlyList<string> TriggeredRules,
    DateTime AssessedAt);

public record AmlFlagDto(
    Guid Id,
    Guid? CustomerProfileId,
    Guid? TransferId,
    string? TransferReference,
    string FlagType,
    string Severity,
    int RiskScore,
    bool IsBlocking,
    string Description,
    bool IsResolved,
    string? ReviewDecision,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    DateTime? ResolvedAt);

public record AmlFlagDetailsDto(
    AmlFlagDto Flag,
    string? ResolutionNote,
    Guid? ReviewedByUserId,
    Guid? ResolvedByUserId,
    string? TransferRiskLevel,
    bool? TransferIsComplianceHold,
    string? TransferComplianceHoldReason);

public class ReviewAmlFlagRequestDto
{
    [Required]
    public RiskReviewDecision Decision { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Note { get; set; } = string.Empty;
}
public class ReassessTransferRiskRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public record ComplianceLimitDto(
    Guid Id,
    CustomerType CustomerType,
    string CountryCode,
    string CurrencyCode,
    decimal DailyLimit,
    decimal MonthlyLimit,
    decimal PerTransferLimit,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public class UpsertComplianceLimitRequestDto
{
    [Required]
    public CustomerType CustomerType { get; set; }

    [Required]
    [MaxLength(10)]
    public string CountryCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal DailyLimit { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal MonthlyLimit { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal PerTransferLimit { get; set; }

    public bool IsActive { get; set; } = true;
}

public record ComplianceLimitDecision(
    bool IsAllowed,
    string? FailureCode,
    string? FailureMessage,
    decimal PerTransferLimit,
    decimal DailyLimit,
    decimal DailyUsed,
    decimal MonthlyLimit,
    decimal MonthlyUsed);
