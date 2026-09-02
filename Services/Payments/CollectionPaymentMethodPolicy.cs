using KorridorX.Models.Enums;

namespace KorridorX.Services.Payments;

public class CollectionPaymentMethodPolicy : ICollectionPaymentMethodPolicy
{
    // This matrix must describe methods that the active remittance provider can
    // actually initiate. Do not expose planned methods here: clients use this
    // policy to create a collection that cannot change payment method later.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PaymentMethod>> SupportedMethods =
        new Dictionary<string, IReadOnlyList<PaymentMethod>>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey("US", "USD")] =
            [
                PaymentMethod.Card
            ],

            [BuildKey("CA", "CAD")] =
            [
                PaymentMethod.Interac
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
