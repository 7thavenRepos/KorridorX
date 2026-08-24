using KorridorX.Dtos.DigitalAssets;

namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetEnablementService
{
    Task<IReadOnlyList<DigitalAssetAdminAssetDto>> GetAssetsAsync(
        CancellationToken ct = default);

    Task<DigitalAssetAdminAssetDto> UpdateAssetAsync(
        string assetCode,
        Guid userId,
        UpdateDigitalAssetEnablementRequestDto request,
        CancellationToken ct = default);

    Task<DigitalAssetAdminNetworkDto> UpdateNetworkAsync(
        Guid assetNetworkId,
        Guid userId,
        UpdateDigitalAssetNetworkRequestDto request,
        CancellationToken ct = default);

    Task<DigitalAssetCountryAvailabilityDto> UpdateCountryAvailabilityAsync(
        string assetCode,
        string countryCode,
        Guid userId,
        UpdateDigitalAssetCountryAvailabilityRequestDto request,
        CancellationToken ct = default);
}