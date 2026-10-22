using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;

namespace KorridorX.Services.WalletWithdrawals;

public enum WalletWithdrawalSettlementDecision
{
    AwaitProvider = 1,
    Capture = 2,
    Release = 3,
    RequireReconciliation = 4
}

// Pure, fail-closed classification: a webhook/request acceptance is not final
// settlement evidence. Only a directly verified provider transaction with
// matching merchant reference, identity, source debit and recipient/fee amounts
// can capture or release wallet funds. No balance updates occur here.
public static class WalletWithdrawalSettlementPolicy
{
    public static WalletWithdrawalSettlementDecision Classify(
        FiatWalletWithdrawal withdrawal, string? pinnedProviderTransactionId,
        RemittanceTransactionStatusResult verified)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);
        ArgumentNullException.ThrowIfNull(verified);

        if (string.IsNullOrWhiteSpace(verified.ProviderTransactionId) ||
            !string.Equals(verified.TransactionType, "payout", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(verified.MerchantReference, withdrawal.Reference, StringComparison.Ordinal) ||
            !string.Equals(verified.CurrencyCode, withdrawal.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(pinnedProviderTransactionId) &&
                !string.Equals(verified.ProviderTransactionId, pinnedProviderTransactionId, StringComparison.Ordinal))
            return WalletWithdrawalSettlementDecision.RequireReconciliation;

        // The original W2A price was from_amount inclusive. Reject both amount
        // mode confusion and drift in what Blaaiz actually paid/charged.
        if (!verified.AmountWithoutFee.HasValue || !verified.ProviderFeeAmount.HasValue ||
            !string.Equals(verified.ProviderFeeCurrencyCode, withdrawal.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
            verified.AmountWithoutFee.Value != withdrawal.ExpectedRecipientAmount ||
            verified.Amount != withdrawal.ExpectedRecipientAmount ||
            verified.ProviderFeeAmount.Value != withdrawal.ProviderFee ||
            verified.AmountWithoutFee.Value + verified.ProviderFeeAmount.Value != withdrawal.TotalWalletDebit ||
            withdrawal.KorridorXFee != 0m ||
            withdrawal.TotalWalletDebit != withdrawal.WithdrawalAmount)
            return WalletWithdrawalSettlementDecision.RequireReconciliation;

        return verified.ProviderStatus?.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => WalletWithdrawalSettlementDecision.Capture,
            "FAILED" or "REJECTED" => WalletWithdrawalSettlementDecision.Release,
            "INITIATED" or "PENDING" or "PROCESSING" => WalletWithdrawalSettlementDecision.AwaitProvider,
            _ => WalletWithdrawalSettlementDecision.RequireReconciliation
        };
    }
}
