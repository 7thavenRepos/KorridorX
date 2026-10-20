using KorridorX.Dtos.Wallets;

namespace KorridorX.Services.Wallets;

public interface IConsumerWalletReceivingAccountService
{
    Task<ConsumerWalletReceivingAccountDto> GetAsync(Guid userId, Guid walletId, CancellationToken ct = default);
    Task<ConsumerWalletReceivingAccountDto> SetupAsync(Guid userId, Guid walletId, CancellationToken ct = default);
}
