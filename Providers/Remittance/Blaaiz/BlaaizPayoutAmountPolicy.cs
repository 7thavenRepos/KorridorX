using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Providers.Remittance.Blaaiz;

// Preserve existing Send/to_amount payouts while allowing W2B2's inclusive
// wallet debit through from_amount. The fields must never be sent together.
public static class BlaaizPayoutAmountPolicy
{
    public static void Apply(RemittancePayoutRequest request, BlaaizPayoutRequest payload)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(payload);
        if (request.DestinationAmount <= 0m)
            throw new InvalidOperationException("Recipient payout amount must be positive.");

        var reference = string.IsNullOrWhiteSpace(request.MerchantReference)
            ? null : request.MerchantReference.Trim();
        if (reference?.Length > 255)
            throw new InvalidOperationException("Blaaiz merchant reference exceeds 255 characters.");

        if (request.FromAmount.HasValue)
        {
            if (request.FromAmount.Value < request.DestinationAmount ||
                !string.Equals(request.SourceCurrencyCode, request.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(reference))
                throw new InvalidOperationException(
                    "Inclusive fiat payout requires same currency, sufficient debit, and a unique merchant reference.");
            payload.FromAmount = request.FromAmount.Value;
            payload.ToAmount = null;
        }
        else
        {
            payload.FromAmount = null;
            payload.ToAmount = request.DestinationAmount;
        }
        payload.MerchantReference = reference;
    }
}
