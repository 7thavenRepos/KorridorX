using KorridorX.Dtos.Wallets;

namespace KorridorX.Services.Wallets;

public interface IConsumerWalletService
{
    Task<IReadOnlyList<ConsumerWalletDto>> GetWalletsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AvailableConsumerWalletAssetDto>> GetAvailableAssetsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<ConsumerWalletDto> CreateWalletAsync(
        Guid userId,
        CreateConsumerWalletRequestDto request,
        CancellationToken ct = default);
}
