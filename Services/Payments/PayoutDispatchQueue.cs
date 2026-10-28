using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Payments;

// A saved Pending payout may be a preflight shell: it is saved before the
// provider-wallet/liquidity checks. Resume only when NO submission attempt or
// provider identity exists. Even a deleted attempt is evidence of possible
// submission and requires reconciliation rather than an automatic retry.
public static class PayoutDispatchQueue
{
    public static IQueryable<Transfer> Unsubmitted(
        IQueryable<Transfer> transfers,
        IQueryable<Payout> payouts,
        IQueryable<PayoutAttempt> attempts) =>
        transfers.Where(transfer =>
            !transfer.IsDeleted &&
            (transfer.Status == TransferStatus.PaymentReceived ||
             transfer.Status == TransferStatus.Processing) &&
            transfer.ProviderTransferId == null &&
            transfer.ProviderReference == null &&
            !payouts.Any(payout => payout.TransferId == transfer.Id &&
                (payout.IsDeleted || payout.Status != PayoutStatus.Pending ||
                 payout.ProviderPayoutId != null || payout.ProviderReference != null ||
                 attempts.Any(attempt => attempt.PayoutId == payout.Id))));
}
