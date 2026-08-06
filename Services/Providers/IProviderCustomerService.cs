using KorridorX.Dtos.Providers;

namespace KorridorX.Services.Providers;

public interface IProviderCustomerService
{
    Task<ProviderCustomerDto> SyncMyCustomerAsync(
        Guid userId,
        SyncProviderCustomerRequestDto request,
        CancellationToken ct = default);

    Task<ProviderCustomerDto> GetMyProviderCustomerAsync(
        Guid userId,
        CancellationToken ct = default);
}
