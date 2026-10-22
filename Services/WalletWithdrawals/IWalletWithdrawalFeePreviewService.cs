using KorridorX.Dtos.WalletWithdrawals;

namespace KorridorX.Services.WalletWithdrawals;

public interface IWalletWithdrawalFeePreviewService
{
    Task<WalletWithdrawalFeePreviewDto> PreviewAsync(Guid userId, Guid walletId, bool business,
        WalletWithdrawalFeePreviewRequestDto request, CancellationToken ct = default);
}
