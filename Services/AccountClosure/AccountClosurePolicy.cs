using KorridorX.Models.AccountClosure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.AccountClosure;

public static class AccountClosurePolicy
{
    public static bool IsOpen(AccountClosureRequestStatus status) =>
        status == AccountClosureRequestStatus.Requested;

    public static bool HasFinancialExposure(decimal settled, decimal available, decimal held) =>
        settled != 0m || available != 0m || held != 0m;

    public static bool IsUnresolvedTransfer(TransferStatus status) =>
        status is not (TransferStatus.Completed or TransferStatus.Cancelled or
            TransferStatus.Refunded or TransferStatus.Rejected);

    public static bool IsUnresolvedWithdrawal(FiatWalletWithdrawalStatus status) =>
        status is not (FiatWalletWithdrawalStatus.Succeeded or
            FiatWalletWithdrawalStatus.FailedReleased or FiatWalletWithdrawalStatus.CancelledReleased);

    public static bool IsUnresolvedCollection(CollectionStatus status) =>
        status is not (CollectionStatus.Successful or CollectionStatus.Failed or
            CollectionStatus.Cancelled or CollectionStatus.Refunded or CollectionStatus.Expired);

    public static bool IsUnresolvedPayout(PayoutStatus status) =>
        status is not (PayoutStatus.Successful or PayoutStatus.Failed);
}
