using KorridorX.Dtos.EmbeddedFinance;

namespace KorridorX.Services.EmbeddedFinance;

public interface IBusinessPricingService
{
    Task<BusinessPricingResolutionDto> ResolveAsync(
        Guid businessProfileId,
        string sourceAssetCode,
        string destinationAssetCode,
        decimal baseCustomerRate,
        DateTime asOf,
        CancellationToken ct = default);
    Task<BusinessPricingPolicyDto> CreatePolicyAsync(
        Guid businessProfileId,
        CreateBusinessPricingPolicyRequestDto request,
        Guid? actionedByUserId = null,
        CancellationToken ct = default);
    Task<BusinessPricingPolicyDto> UpdatePolicyAsync(
        Guid businessProfileId,
        Guid policyId,
        UpdateBusinessPricingPolicyRequestDto request,
        Guid? actionedByUserId = null,
        CancellationToken ct = default);
    Task<BusinessPricingPolicyDto> DisablePolicyAsync(
        Guid businessProfileId,
        Guid policyId,
        Guid? actionedByUserId = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<BusinessPricingPolicyDto>> GetPoliciesAsync(
        Guid businessProfileId,
        CancellationToken ct = default);
    Task<IReadOnlyList<BusinessPricingPerformanceRowDto>> GetPerformanceAsync(
        Guid businessProfileId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}
