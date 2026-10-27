using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
namespace KorridorX.Services.Payments;
public class CollectionPaymentMethodPolicy : ICollectionPaymentMethodPolicy
{
    public IReadOnlyList<PaymentMethod> GetSupportedMethods(string sourceCountryCode, string sourceCurrencyCode)
    {
        var currency = BlaaizCurrencyRequirements.Find(sourceCurrencyCode);
        if (currency is null || !currency.TransferCollectionEnabled ||
            !BlaaizCurrencyRequirements.SupportsCountry(sourceCurrencyCode, sourceCountryCode)) return [];
        return currency.CollectionMethods.Where(x => x is PaymentMethod.Card or PaymentMethod.Interac).ToArray();
    }
    public bool IsSupported(string sourceCountryCode, string sourceCurrencyCode, PaymentMethod paymentMethod) =>
        GetSupportedMethods(sourceCountryCode, sourceCurrencyCode).Contains(paymentMethod);
}
