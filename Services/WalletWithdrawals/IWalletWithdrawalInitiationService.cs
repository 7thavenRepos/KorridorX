using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Infrastructure;

namespace KorridorX.Services.WalletWithdrawals;

public interface IWalletWithdrawalInitiationService
{
    Task<WalletWithdrawalDetailsDto> InitiateAsync(Guid userId, Guid walletId, bool business,
        InitiateWalletWithdrawalRequestDto request, string? idempotencyKey, CancellationToken ct = default);
    Task<WalletWithdrawalDetailsDto> GetAsync(Guid userId, Guid walletId, Guid withdrawalId,
        bool business, CancellationToken ct = default);
    Task<PagedResult<WalletWithdrawalDetailsDto>> ListAsync(Guid userId, Guid walletId,
        bool business, int page, int pageSize, CancellationToken ct = default);
}
