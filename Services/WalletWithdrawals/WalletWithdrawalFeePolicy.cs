using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Services.WalletWithdrawals;

public sealed record WalletWithdrawalFeeAmounts(decimal ProviderFee, decimal RecipientAmount);

// Monetary policy is deliberately independent of HTTP and the database, so
// malformed or unexpectedly changed provider responses can be tested locally.
public static class WalletWithdrawalFeePolicy
{
    private const decimal MaximumAmount = 999999999999999999m;

    public static void ValidateAmount(decimal amount, decimal availableBalance, int decimalPlaces)
    {
        if (decimalPlaces is < 0 or > 18)
            throw new InvalidOperationException("Invalid configured wallet-currency precision.");
        if (amount <= 0m || amount > MaximumAmount || !HasPrecision(amount, decimalPlaces))
            throw new InvalidOperationException($"Enter a positive withdrawal amount with at most {decimalPlaces} decimal places.");
        if (availableBalance < 0m || amount > availableBalance)
            throw new InvalidOperationException("The withdrawal amount exceeds the available wallet balance.");
    }

    public static WalletWithdrawalFeeAmounts Evaluate(
        BlaaizFeeBreakdownResponse response, PaymentMethod paymentMethod,
        decimal walletDebit, int decimalPlaces)
    {
        ArgumentNullException.ThrowIfNull(response);
        ValidateAmount(walletDebit, walletDebit, decimalPlaces);
        var code = paymentMethod switch
        {
            PaymentMethod.BankTransfer => "BANK_TRANSFER",
            PaymentMethod.Interac => "INTERAC",
            PaymentMethod.Ach => "ACH",
            PaymentMethod.Wire => "WIRE",
            _ => throw new InvalidOperationException("An executable same-currency payout method is required.")
        };

        var matches = response.PayoutFees?.Where(x => string.Equals(
            x.Method?.Trim().Replace('-', '_'), code, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches is null || matches.Length != 1)
            throw InvalidQuote("The selected payout method does not have exactly one fee breakdown.");

        var selected = matches[0];
        if (!selected.YouSend.HasValue || !selected.RecipientGets.HasValue ||
            !selected.TotalFees.HasValue || !selected.MethodFee.HasValue || !selected.BaseFee.HasValue ||
            !selected.NetAmountToSend.HasValue)
            throw InvalidQuote("A required monetary amount is missing.");

        var send = selected.YouSend.Value;
        var receive = selected.RecipientGets.Value;
        var totalFee = selected.TotalFees.Value;
        var methodFee = selected.MethodFee.Value;
        var baseFee = selected.BaseFee.Value;
        var netAmount = selected.NetAmountToSend.Value;

        if (!new[] { send, receive, totalFee, methodFee, baseFee, netAmount }.All(x =>
                x >= 0m && x <= MaximumAmount && HasPrecision(x, decimalPlaces)) ||
            send != walletDebit || receive <= 0m ||
            methodFee + baseFee != totalFee ||
            walletDebit - totalFee != receive || netAmount != receive ||
            selected.Rate is decimal rate && rate != 1m ||
            selected.AmountIsYouSend == false || selected.AmountIsRecipientGets == true)
            throw InvalidQuote("The provider amounts do not reconcile to a same-currency inclusive wallet debit.");

        // Every component of total_fees is Blaaiz's fee, not KorridorX revenue.
        return new WalletWithdrawalFeeAmounts(totalFee, receive);
    }

    private static bool HasPrecision(decimal value, int places) =>
        decimal.Round(value, places, MidpointRounding.AwayFromZero) == value;

    private static InvalidOperationException InvalidQuote(string reason) => new(
        "Blaaiz could not provide a reliable same-currency withdrawal quote. " + reason +
        " No funds have been reserved or moved.");
}
