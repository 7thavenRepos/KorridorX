using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public record BusinessPricingResolutionDto(
    decimal BaseCustomerRate,
    decimal CustomerRate,
    Guid? BusinessPricingPolicyId,
    decimal? BusinessMarkupPercentage,
    BusinessPricingAdjustmentType? AdjustmentType = null,
    decimal? AdjustmentValue = null,
    decimal BusinessRevenueRate = 0m);

public sealed record BusinessPricingPolicyDto(
    Guid Id,
    string SourceAssetCode,
    string DestinationAssetCode,
    decimal MarkupPercentage,
    decimal? MinimumCustomerRate,
    decimal? MaximumCustomerRate,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    BusinessPricingAdjustmentType AdjustmentType = BusinessPricingAdjustmentType.Percentage,
    decimal AdjustmentValue = 0m);

public sealed record CreateBusinessPricingPolicyRequestDto(
    string SourceAssetCode,
    string DestinationAssetCode,
    decimal MarkupPercentage,
    decimal? MinimumCustomerRate = null,
    decimal? MaximumCustomerRate = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null,
    BusinessPricingAdjustmentType AdjustmentType = BusinessPricingAdjustmentType.Percentage,
    decimal? AdjustmentValue = null);

public sealed record UpdateBusinessPricingPolicyRequestDto(
    decimal MarkupPercentage,
    decimal? MinimumCustomerRate = null,
    decimal? MaximumCustomerRate = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null,
    bool IsActive = true,
    BusinessPricingAdjustmentType AdjustmentType = BusinessPricingAdjustmentType.Percentage,
    decimal? AdjustmentValue = null);

public sealed record BusinessPricingPerformanceRowDto(
    string SourceAssetCode,
    string DestinationAssetCode,
    int CompletedTrades,
    decimal SourceVolume,
    decimal CustomerDestinationVolume,
    decimal BaseDestinationVolume,
    decimal RealizedBusinessRevenue,
    decimal AverageEffectiveMarkupPercentage);
