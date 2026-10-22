using KorridorX.Dtos.WalletWithdrawals;

namespace KorridorX.Services.WalletWithdrawals;

public interface IWalletWithdrawalSetupService
{
    Task<IReadOnlyList<WalletWithdrawalMethodDto>> GetMethodsAsync(Guid userId, Guid walletId, bool business, CancellationToken ct = default);
    Task<IReadOnlyList<WalletWithdrawalDestinationDto>> GetDestinationsAsync(Guid userId, Guid walletId, bool business, CancellationToken ct = default);
    Task<WalletWithdrawalDestinationDto> SaveDestinationAsync(Guid userId, Guid walletId, bool business,
        SaveWalletWithdrawalDestinationRequestDto request, CancellationToken ct = default);
    Task<WalletWithdrawalDestinationDto> DisableDestinationAsync(Guid userId, Guid walletId, Guid destinationId, bool business,
        DisableWalletWithdrawalDestinationRequestDto request, CancellationToken ct = default);
}
