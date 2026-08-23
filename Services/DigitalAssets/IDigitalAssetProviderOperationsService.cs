using KorridorX.Dtos.DigitalAssets;

namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetProviderOperationsService
{
    Task<IReadOnlyList<DigitalAssetProviderConfigurationDto>> GetConfigurationsAsync(
        CancellationToken ct = default);

    Task<DigitalAssetProviderConfigurationDto> UpsertConfigurationAsync(
        Guid userId,
        UpsertDigitalAssetProviderConfigurationRequestDto request,
        CancellationToken ct = default);

    Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        CancellationToken ct = default);

    Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetFeeExceptionDto>> GetFeeExceptionsAsync(
        decimal minimumAbsoluteVariance = 0.00000001m,
        int take = 100,
        CancellationToken ct = default);
}
