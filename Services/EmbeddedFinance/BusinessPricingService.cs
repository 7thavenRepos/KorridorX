using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class BusinessPricingService : IBusinessPricingService
{
    private readonly AppDbContext _db;

    public BusinessPricingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BusinessPricingResolutionDto> ResolveAsync(
        Guid businessProfileId,
        string sourceAssetCode,
        string destinationAssetCode,
        decimal baseCustomerRate,
        DateTime asOf,
        CancellationToken ct = default)
    {
        if (businessProfileId == Guid.Empty)
            throw new ArgumentException("Business profile is required.", nameof(businessProfileId));
        if (baseCustomerRate <= 0m)
            throw new InvalidOperationException("Base customer rate must be greater than zero.");

        var source = Normalize(sourceAssetCode);
        var destination = Normalize(destinationAssetCode);

        var policy = await _db.BusinessPricingPolicies
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.SourceAssetCode == source &&
                x.DestinationAssetCode == destination &&
                x.IsActive &&
                !x.IsDeleted &&
                x.EffectiveFrom <= asOf &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > asOf))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (policy is null)
            return new BusinessPricingResolutionDto(
                baseCustomerRate,
                baseCustomerRate,
                null,
                null);

        var adjustmentValue = policy.AdjustmentValue;
        var customerRate = policy.AdjustmentType switch
        {
            BusinessPricingAdjustmentType.Percentage =>
                baseCustomerRate * (1m - (adjustmentValue / 100m)),
            BusinessPricingAdjustmentType.BasisPoints =>
                baseCustomerRate * (1m - (adjustmentValue / 10000m)),
            BusinessPricingAdjustmentType.FixedSpread =>
                baseCustomerRate - adjustmentValue,
            _ => throw new InvalidOperationException(
                "The configured business pricing adjustment type is invalid.")
        };

        if (policy.MinimumCustomerRate is decimal min && customerRate < min)
            customerRate = min;
        if (policy.MaximumCustomerRate is decimal max && customerRate > max)
            customerRate = max;

        customerRate = Math.Round(customerRate, 8, MidpointRounding.AwayFromZero);

        if (customerRate <= 0m)
            throw new InvalidOperationException(
                "The configured business pricing policy produces an invalid customer rate.");

        if (customerRate > baseCustomerRate)
            throw new InvalidOperationException(
                "The configured business pricing policy cannot produce a customer rate above the platform base rate.");

        var revenueRate = Math.Round(baseCustomerRate - customerRate, 8, MidpointRounding.AwayFromZero);
        var effectiveMarkupPercentage = Math.Round(
            (revenueRate / baseCustomerRate) * 100m,
            6,
            MidpointRounding.AwayFromZero);

        return new BusinessPricingResolutionDto(
            baseCustomerRate,
            customerRate,
            policy.Id,
            effectiveMarkupPercentage,
            policy.AdjustmentType,
            adjustmentValue,
            revenueRate);
    }


    public async Task<BusinessPricingPolicyDto> CreatePolicyAsync(
        Guid businessProfileId,
        CreateBusinessPricingPolicyRequestDto request,
        Guid? actionedByUserId = null,
        CancellationToken ct = default)
    {
        await EnsureBusinessProfileAsync(businessProfileId, ct);

        var source = Normalize(request.SourceAssetCode);
        var destination = Normalize(request.DestinationAssetCode);
        var effectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow;
        var adjustmentValue = request.AdjustmentValue ?? request.MarkupPercentage;
        ValidateAdjustment(request.AdjustmentType, adjustmentValue);
        var normalizedMarkup = NormalizeMarkupPercentage(request.AdjustmentType, adjustmentValue);

        await ValidatePolicyAsync(
            businessProfileId,
            null,
            source,
            destination,
            normalizedMarkup,
            request.MinimumCustomerRate,
            request.MaximumCustomerRate,
            effectiveFrom,
            request.EffectiveTo,
            true,
            ct);

        var policy = new BusinessPricingPolicy
        {
            BusinessProfileId = businessProfileId,
            SourceAssetCode = source,
            DestinationAssetCode = destination,
            MarkupPercentage = normalizedMarkup,
            AdjustmentType = request.AdjustmentType,
            AdjustmentValue = adjustmentValue,
            MinimumCustomerRate = request.MinimumCustomerRate,
            MaximumCustomerRate = request.MaximumCustomerRate,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = true,
            CreatedByUserId = actionedByUserId
        };

        _db.BusinessPricingPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(policy);
    }

    public async Task<BusinessPricingPolicyDto> UpdatePolicyAsync(
        Guid businessProfileId,
        Guid policyId,
        UpdateBusinessPricingPolicyRequestDto request,
        Guid? actionedByUserId = null,
        CancellationToken ct = default)
    {
        var policy = await GetTrackedPolicyAsync(businessProfileId, policyId, ct);
        var effectiveFrom = request.EffectiveFrom ?? policy.EffectiveFrom;
        var adjustmentValue = request.AdjustmentValue ?? request.MarkupPercentage;
        ValidateAdjustment(request.AdjustmentType, adjustmentValue);
        var normalizedMarkup = NormalizeMarkupPercentage(request.AdjustmentType, adjustmentValue);

        await ValidatePolicyAsync(
            businessProfileId,
            policy.Id,
            policy.SourceAssetCode,
            policy.DestinationAssetCode,
            normalizedMarkup,
            request.MinimumCustomerRate,
            request.MaximumCustomerRate,
            effectiveFrom,
            request.EffectiveTo,
            request.IsActive,
            ct);

        policy.MarkupPercentage = normalizedMarkup;
        policy.AdjustmentType = request.AdjustmentType;
        policy.AdjustmentValue = adjustmentValue;
        policy.MinimumCustomerRate = request.MinimumCustomerRate;
        policy.MaximumCustomerRate = request.MaximumCustomerRate;
        policy.EffectiveFrom = effectiveFrom;
        policy.EffectiveTo = request.EffectiveTo;
        policy.IsActive = request.IsActive;
        policy.LastUpdatedAt = DateTime.UtcNow;
        policy.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(policy);
    }

    public async Task<BusinessPricingPolicyDto> DisablePolicyAsync(
        Guid businessProfileId,
        Guid policyId,
        Guid? actionedByUserId = null,
        CancellationToken ct = default)
    {
        var policy = await GetTrackedPolicyAsync(businessProfileId, policyId, ct);

        if (!policy.IsActive)
            return ToPolicyDto(policy);

        policy.IsActive = false;
        policy.LastUpdatedAt = DateTime.UtcNow;
        policy.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(policy);
    }

    public async Task<IReadOnlyList<BusinessPricingPolicyDto>> GetPoliciesAsync(
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        await EnsureBusinessProfileAsync(businessProfileId, ct);

        return await _db.BusinessPricingPolicies
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == businessProfileId && !x.IsDeleted)
            .OrderBy(x => x.SourceAssetCode)
            .ThenBy(x => x.DestinationAssetCode)
            .ThenByDescending(x => x.EffectiveFrom)
            .Select(x => new BusinessPricingPolicyDto(
                x.Id,
                x.SourceAssetCode,
                x.DestinationAssetCode,
                x.MarkupPercentage,
                x.MinimumCustomerRate,
                x.MaximumCustomerRate,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.IsActive,
                x.CreatedAt,
                x.LastUpdatedAt,
                x.AdjustmentType,
                x.AdjustmentValue))
            .ToListAsync(ct);
    }

    private async Task ValidatePolicyAsync(
        Guid businessProfileId,
        Guid? currentPolicyId,
        string sourceAssetCode,
        string destinationAssetCode,
        decimal markupPercentage,
        decimal? minimumCustomerRate,
        decimal? maximumCustomerRate,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        bool isActive,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceAssetCode) ||
            string.IsNullOrWhiteSpace(destinationAssetCode))
            throw new InvalidOperationException("Source and destination assets are required.");

        if (sourceAssetCode == destinationAssetCode)
            throw new InvalidOperationException("Source and destination assets must be different.");

        if (markupPercentage < 0m || markupPercentage >= 100m)
            throw new InvalidOperationException("Business markup percentage must be between 0 and 100.");

        if (minimumCustomerRate.HasValue && minimumCustomerRate.Value <= 0m)
            throw new InvalidOperationException("Minimum customer rate must be greater than zero.");

        if (maximumCustomerRate.HasValue && maximumCustomerRate.Value <= 0m)
            throw new InvalidOperationException("Maximum customer rate must be greater than zero.");

        if (minimumCustomerRate.HasValue &&
            maximumCustomerRate.HasValue &&
            minimumCustomerRate.Value > maximumCustomerRate.Value)
            throw new InvalidOperationException(
                "Minimum customer rate cannot be greater than maximum customer rate.");

        if (effectiveTo.HasValue && effectiveTo.Value <= effectiveFrom)
            throw new InvalidOperationException("EffectiveTo must be later than EffectiveFrom.");

        var assetCount = await _db.Assets
            .AsNoTracking()
            .CountAsync(x =>
                (x.Code == sourceAssetCode || x.Code == destinationAssetCode) &&
                x.IsSupported,
                ct);

        if (assetCount != 2)
            throw new InvalidOperationException(
                "Both source and destination assets must be supported assets.");

        if (!isActive)
            return;

        var overlaps = await _db.BusinessPricingPolicies
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.SourceAssetCode == sourceAssetCode &&
                x.DestinationAssetCode == destinationAssetCode &&
                x.IsActive &&
                !x.IsDeleted &&
                (!currentPolicyId.HasValue || x.Id != currentPolicyId.Value) &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > effectiveFrom) &&
                (!effectiveTo.HasValue || effectiveTo.Value > x.EffectiveFrom),
                ct);

        if (overlaps)
            throw new InvalidOperationException(
                "An active business pricing policy already overlaps this asset pair and effective period.");
    }

    private async Task EnsureBusinessProfileAsync(Guid businessProfileId, CancellationToken ct)
    {
        if (businessProfileId == Guid.Empty)
            throw new ArgumentException("Business profile is required.", nameof(businessProfileId));

        var exists = await _db.BusinessProfiles
            .AsNoTracking()
            .AnyAsync(x => x.Id == businessProfileId && !x.IsDeleted, ct);

        if (!exists)
            throw new InvalidOperationException("Business profile not found.");
    }

    private async Task<BusinessPricingPolicy> GetTrackedPolicyAsync(
        Guid businessProfileId,
        Guid policyId,
        CancellationToken ct) =>
        await _db.BusinessPricingPolicies
            .FirstOrDefaultAsync(x =>
                x.Id == policyId &&
                x.BusinessProfileId == businessProfileId &&
                !x.IsDeleted,
                ct)
        ?? throw new InvalidOperationException("Business pricing policy not found.");

    private static BusinessPricingPolicyDto ToPolicyDto(BusinessPricingPolicy policy) =>
        new(
            policy.Id,
            policy.SourceAssetCode,
            policy.DestinationAssetCode,
            policy.MarkupPercentage,
            policy.MinimumCustomerRate,
            policy.MaximumCustomerRate,
            policy.EffectiveFrom,
            policy.EffectiveTo,
            policy.IsActive,
            policy.CreatedAt,
            policy.LastUpdatedAt,
            policy.AdjustmentType,
            policy.AdjustmentValue);

    public async Task<IReadOnlyList<BusinessPricingPerformanceRowDto>> GetPerformanceAsync(
    Guid businessProfileId,
    DateTime? from = null,
    DateTime? to = null,
    CancellationToken ct = default)
    {
        await EnsureBusinessProfileAsync(businessProfileId, ct);

        var query = _db.InstantTrades
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.Status == InstantTradeStatus.Completed &&
                !x.IsDeleted);

        if (from.HasValue)
            query = query.Where(x => x.CompletedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.CompletedAt < to.Value);

        var rows = await query
            .Select(x => new
            {
                x.InstantPair.SourceAssetCode,
                x.InstantPair.DestinationAssetCode,
                x.SourceAmount,
                x.DestinationAmount,
                x.BaseDestinationAmount,
                x.BusinessRevenueAmount,
                x.BaseCustomerRate,
                x.CustomerRate
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => new
            {
                x.SourceAssetCode,
                x.DestinationAssetCode
            })
            .Select(g => new BusinessPricingPerformanceRowDto(
                g.Key.SourceAssetCode,
                g.Key.DestinationAssetCode,
                g.Count(),
                g.Sum(x => x.SourceAmount),
                g.Sum(x => x.DestinationAmount),
                g.Sum(x => x.BaseDestinationAmount),
                g.Sum(x => x.BusinessRevenueAmount),
                g.Average(x =>
                    x.BaseCustomerRate > 0m
                        ? ((x.BaseCustomerRate - x.CustomerRate) /
                           x.BaseCustomerRate) * 100m
                        : 0m)))
            .OrderBy(x => x.SourceAssetCode)
            .ThenBy(x => x.DestinationAssetCode)
            .ToList();
    }

    private static void ValidateAdjustment(
        BusinessPricingAdjustmentType adjustmentType,
        decimal adjustmentValue)
    {
        if (adjustmentValue < 0m)
            throw new InvalidOperationException("Business pricing adjustment cannot be negative.");

        switch (adjustmentType)
        {
            case BusinessPricingAdjustmentType.Percentage:
                if (adjustmentValue >= 100m)
                    throw new InvalidOperationException("Percentage business pricing adjustment must be below 100.");
                break;
            case BusinessPricingAdjustmentType.BasisPoints:
                if (adjustmentValue >= 10000m)
                    throw new InvalidOperationException("Basis-point business pricing adjustment must be below 10000.");
                break;
            case BusinessPricingAdjustmentType.FixedSpread:
                break;
            default:
                throw new InvalidOperationException("Business pricing adjustment type is invalid.");
        }
    }

    private static decimal NormalizeMarkupPercentage(
        BusinessPricingAdjustmentType adjustmentType,
        decimal adjustmentValue) =>
        adjustmentType switch
        {
            BusinessPricingAdjustmentType.Percentage => adjustmentValue,
            BusinessPricingAdjustmentType.BasisPoints => adjustmentValue / 100m,
            BusinessPricingAdjustmentType.FixedSpread => 0m,
            _ => 0m
        };

    private static string Normalize(string value) =>
        (value ?? "").Trim().ToUpperInvariant();
}
