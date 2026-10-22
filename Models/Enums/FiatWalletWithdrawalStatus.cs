namespace KorridorX.Models.Enums;

// A requested withdrawal remains on the internal ledger until verified
// provider settlement. W2B1 only creates AwaitingExecution records; it does
// not dispatch payouts or transition them to any terminal state.
public enum FiatWalletWithdrawalStatus
{
    AwaitingExecution = 1,
    Processing = 2,
    Succeeded = 3,
    FailedReleased = 4,
    RequiresReconciliation = 5,
    CancelledReleased = 6
}
