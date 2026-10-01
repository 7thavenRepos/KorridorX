using KorridorX.Dtos.Wallets;

namespace KorridorX.Services.Wallets;

public interface IConsumerWalletValuationService
{
    Task<ConsumerWalletValuationDto> GetValuationAsync(
        Guid userId,
        string baseAssetCode,
        CancellationToken ct = default);
}
