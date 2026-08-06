using KorridorX.Models.Enums;

namespace KorridorX.Services.Payments;

public class CollectionPaymentMethodPolicy : ICollectionPaymentMethodPolicy
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PaymentMethod>> SupportedMethods =
        new Dictionary<string, IReadOnlyList<PaymentMethod>>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey("US", "USD")] =
            [
                PaymentMethod.Ach,
                PaymentMethod.Wire,
                PaymentMethod.Card,
                PaymentMethod.BankTransfer,
                PaymentMethod.VirtualAccount
            ],

            [BuildKey("CA", "CAD")] =
            [
                PaymentMethod.Interac,
                PaymentMethod.BankTransfer
            ]
        };

    public IReadOnlyList<PaymentMethod> GetSupportedMethods(
        string sourceCountryCode,
        string sourceCurrencyCode)
    {
        var key = BuildKey(sourceCountryCode, sourceCurrencyCode);

        return SupportedMethods.TryGetValue(key, out var methods)
            ? methods
            : [];
    }

    public bool IsSupported(
        string sourceCountryCode,
        string sourceCurrencyCode,
        PaymentMethod paymentMethod)
    {
        return GetSupportedMethods(sourceCountryCode, sourceCurrencyCode)
            .Contains(paymentMethod);
    }

    private static string BuildKey(
        string sourceCountryCode,
        string sourceCurrencyCode)
    {
        return $"{Normalize(sourceCountryCode)}:{Normalize(sourceCurrencyCode)}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
