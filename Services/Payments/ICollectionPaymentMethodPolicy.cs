using KorridorX.Models.Enums;

namespace KorridorX.Services.Payments;

public interface ICollectionPaymentMethodPolicy
{
    IReadOnlyList<PaymentMethod> GetSupportedMethods(
        string sourceCountryCode,
        string sourceCurrencyCode);

    bool IsSupported(
        string sourceCountryCode,
        string sourceCurrencyCode,
        PaymentMethod paymentMethod);
}
