using KorridorX.Models.Enums;
using KorridorX.Models.Providers;

namespace KorridorX.Services.Payments;

public static class PayoutDestinationCapabilityPolicy
{
    private static readonly IReadOnlyDictionary<ProviderCode, IReadOnlySet<PayoutDestinationType>> SupportedDestinations =
        new Dictionary<ProviderCode, IReadOnlySet<PayoutDestinationType>>
        {
            [ProviderCode.Blaaiz] = new HashSet<PayoutDestinationType>
            {
                PayoutDestinationType.RecipientBankAccount,
                PayoutDestinationType.BusinessBeneficiaryBankAccount
            }
        };

    public static bool IsSupported(string providerCode, PayoutDestinationType destinationType)
    {
        if (string.IsNullOrWhiteSpace(providerCode) ||
            !Enum.TryParse<ProviderCode>(providerCode.Trim(), true, out var parsedProviderCode))
        {
            return false;
        }

        return SupportedDestinations.TryGetValue(parsedProviderCode, out var destinations) &&
               destinations.Contains(destinationType);
    }

    public static void EnsureSupported(string providerCode, PayoutDestinationType destinationType)
    {
        if (IsSupported(providerCode, destinationType))
        {
            return;
        }

        if (destinationType is PayoutDestinationType.RecipientMobileWallet or
            PayoutDestinationType.BusinessBeneficiaryMobileWallet)
        {
            throw new InvalidOperationException(
                "Mobile-wallet payouts are not supported by the selected provider. Select a bank-account destination.");
        }

        throw new InvalidOperationException(
            "The selected payout destination is not supported by the selected provider.");
    }
}
