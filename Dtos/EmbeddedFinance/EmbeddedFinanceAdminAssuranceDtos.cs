using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record EmbeddedFinanceAdminAssuranceDto(
    EmbeddedFinanceAdminAssuranceBusinessDto? Business,
    EmbeddedFinanceAdminPricingAssuranceDto Pricing,
    EmbeddedFinanceAdminSecurityAssuranceDto Security,
    EmbeddedFinanceAdminComplianceAssuranceDto Compliance,
    EmbeddedFinanceAdminOperationalAssuranceDto Operations,
    DateTime GeneratedAt);

public sealed record EmbeddedFinanceAdminAssuranceBusinessDto(
    Guid BusinessProfileId,
    string BusinessName,
    string CountryCode,
    string KybStatus);

public sealed record EmbeddedFinanceAdminPricingAssuranceDto(
    bool BusinessSelected,
    int PolicyCount,
    int ActivePolicyCount,
    int CompletedTrades,
    decimal RealizedBusinessRevenue,
    IReadOnlyList<EmbeddedFinanceAdminPricingPolicyDto> Policies,
    IReadOnlyList<EmbeddedFinanceAdminPricingPerformanceDto> Performance);

public sealed record EmbeddedFinanceAdminPricingPolicyDto(
    Guid Id,
    string SourceAssetCode,
    string DestinationAssetCode,
    string AdjustmentType,
    decimal AdjustmentValue,
    decimal? MinimumCustomerRate,
    decimal? MaximumCustomerRate,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminPricingPerformanceDto(
    string SourceAssetCode,
    string DestinationAssetCode,
    int CompletedTrades,
    decimal SourceVolume,
    decimal CustomerDestinationVolume,
    decimal BaseDestinationVolume,
    decimal RealizedBusinessRevenue,
    decimal AverageEffectiveMarkupPercentage);

public sealed record EmbeddedFinanceAdminSecurityAssuranceDto(
    int ApiApplications,
    int ActiveApiApplications,
    int ApplicationsWithIpAllowlist,
    int ApplicationsWithoutIpAllowlist,
    int ActiveCredentials,
    int ExpiringCredentialsNext30Days,
    int ExpiredCredentials,
    int IdempotencyRecordsLast24Hours,
    DateTime? LatestIdempotencyAt,
    IReadOnlyList<EmbeddedFinanceAdminApplicationSecurityDto> Applications);

public sealed record EmbeddedFinanceAdminApplicationSecurityDto(
    Guid Id,
    string Name,
    string Status,
    EmbeddedFinanceScope Scopes,
    int AllowedIpCount,
    int CredentialCount,
    int ActiveCredentialCount,
    int ExpiringCredentialCount,
    DateTime? LastAuthenticatedAt,
    DateTime? LastCredentialUsedAt,
    int IdempotencyRecordsLast24Hours,
    DateTime? LatestIdempotencyAt);

public sealed record EmbeddedFinanceAdminComplianceAssuranceDto(
    string? KybStatus,
    int OpenCases,
    int BlockingCases,
    int ScreeningsLast30Days,
    int BlockingScreenings,
    int FailedScreeningsLast24Hours,
    int ActiveBusinessRestrictions,
    int ActiveCustomerRestrictions,
    DateTime? LatestScreeningAt);

public sealed record EmbeddedFinanceAdminOperationalAssuranceDto(
    int FailedProviderRequestsLast24Hours,
    int StaleProviderTransactions,
    int PendingProviderMappings,
    int FailedProviderMappings,
    int PendingWebhookDeliveries,
    int RetryWebhookDeliveries,
    int DeadLetterWebhookDeliveries,
    DateTime? OldestPendingWebhookAt,
    bool ProviderMetricsAreGlobal);
