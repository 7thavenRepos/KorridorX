using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Providers.DigitalAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetComplianceGate : IDigitalAssetComplianceGate
{
    private readonly AppDbContext? _db;
    private readonly IEnumerable<IDigitalAssetAddressRiskProvider> _riskProviders;
    private readonly DigitalAssetComplianceOptions _options;

    public DigitalAssetComplianceGate()
    {
        _riskProviders = Array.Empty<IDigitalAssetAddressRiskProvider>();
        _options = new DigitalAssetComplianceOptions();
    }

    public DigitalAssetComplianceGate(
        AppDbContext db,
        IEnumerable<IDigitalAssetAddressRiskProvider> riskProviders,
        IOptions<DigitalAssetComplianceOptions> options)
    {
        _db = db;
        _riskProviders = riskProviders;
        _options = options.Value;
    }

    public Task EnsureDepositAllowedAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        string assetCode,
        string networkCode,
        decimal amount,
        string? sourceAddress,
        CancellationToken ct = default) =>
        ScreenIfRequiredAsync(
            businessProfileId,
            businessCustomerId,
            assetCode,
            networkCode,
            amount,
            sourceAddress,
            DigitalAssetAddressScreeningDirection.DepositSource,
            ct);

    public Task EnsureWithdrawalAllowedAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        string assetCode,
        string networkCode,
        decimal amount,
        string destinationAddress,
        CancellationToken ct = default) =>
        ScreenIfRequiredAsync(
            businessProfileId,
            businessCustomerId,
            assetCode,
            networkCode,
            amount,
            destinationAddress,
            DigitalAssetAddressScreeningDirection.WithdrawalDestination,
            ct);

    private async Task ScreenIfRequiredAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        string assetCode,
        string networkCode,
        decimal amount,
        string? address,
        DigitalAssetAddressScreeningDirection direction,
        CancellationToken ct)
    {
        if (_db is null)
            return;

        if (string.IsNullOrWhiteSpace(address))
        {
            if (_options.RequireAddressScreening)
                throw new InvalidOperationException("Address screening requires a blockchain address.");
            return;
        }

        var network = await _db!.AssetNetworks.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AssetCode == assetCode &&
                x.NetworkCode == networkCode,
                ct)
            ?? throw new InvalidOperationException("Asset network not found for digital-asset screening.");

        var freshAfter = DateTime.UtcNow.AddMinutes(-Math.Max(1, _options.AddressRiskCacheMinutes));

        var cached = await _db!.DigitalAssetAddressRiskAssessments.AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.AssetNetworkId == network.Id &&
                x.Address == address &&
                x.Direction == direction &&
                x.AssessedAt >= freshAfter &&
                (!x.ExpiresAt.HasValue || x.ExpiresAt > DateTime.UtcNow) &&
                !x.IsDeleted)
            .OrderByDescending(x => x.AssessedAt)
            .FirstOrDefaultAsync(ct);

        if (cached is not null)
        {
            if (cached.IsBlocking || cached.RiskScore >= _options.BlockingRiskScore)
                throw new InvalidOperationException("Digital-asset address failed compliance screening.");
            return;
        }

        var provider = _riskProviders.FirstOrDefault();
        if (provider is null)
        {
            if (_options.RequireAddressScreening)
                throw new InvalidOperationException("Digital-asset address screening provider is not configured.");
            return;
        }

        var result = await provider.ScreenAsync(
            assetCode,
            networkCode,
            address,
            direction,
            ct);

        var blocking = result.IsBlocking || result.RiskScore >= _options.BlockingRiskScore;

        var assessment = new DigitalAssetAddressRiskAssessment
        {
            BusinessProfileId = businessProfileId,
            BusinessCustomerId = businessCustomerId,
            AssetNetworkId = network.Id,
            AssetCode = assetCode,
            Address = address,
            Direction = direction,
            ProviderCode = provider.RiskProviderCode,
            ProviderReference = result.ProviderReference,
            RiskScore = result.RiskScore,
            RiskLevel = result.RiskLevel,
            IsBlocking = blocking,
            ReasonsJson = JsonSerializer.Serialize(result.Reasons),
            RawResultJson = result.RawResultJson,
            AssessedAt = DateTime.UtcNow,
            ExpiresAt = result.ExpiresAt
        };

        _db!.DigitalAssetAddressRiskAssessments.Add(assessment);
        await _db!.SaveChangesAsync(ct);

        if (blocking)
            throw new InvalidOperationException("Digital-asset address failed compliance screening.");
    }
}
