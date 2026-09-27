using KorridorX.Dtos.Notifications;

namespace KorridorX.Services.Notifications;

public interface IMobilePushDeviceService
{
    Task<MobilePushDeviceDto> RegisterAsync(
        Guid userId,
        RegisterMobilePushDeviceRequestDto request,
        CancellationToken ct = default);

    Task<UnregisterMobilePushDeviceResultDto> UnregisterAsync(
        Guid userId,
        UnregisterMobilePushDeviceRequestDto request,
        CancellationToken ct = default);
}
