using System.Linq.Expressions;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.BusinessBeneficiaries;

namespace KorridorX.Services.BusinessTransfers;

/// <summary>
/// Prefilter by business, transfer type, receiving country and bank currency.
/// Local payout-field validation is applied after materialization (EF cannot
/// translate the currency-specific field validators); provider mappings are
/// still enforced when a transfer is created.
/// </summary>
public static class BusinessTransferRecipientEligibilityPolicy
{
    public static Expression<Func<BusinessBeneficiary, bool>> ForQuote(
        Guid businessProfileId,
        BusinessBeneficiaryType expectedType,
        string destinationCurrency,
        string[] receivingCountryCodes)
    {
        ArgumentNullException.ThrowIfNull(receivingCountryCodes);
        var locallyValidatedForeignBank = BlaaizCurrencyRequirements.IsForeignBank(destinationCurrency);
        var interacEmail = destinationCurrency == "CAD";
        return beneficiary =>
            !beneficiary.IsDeleted && beneficiary.IsActive &&
            beneficiary.BusinessProfileId == businessProfileId &&
            beneficiary.BeneficiaryType == expectedType &&
            receivingCountryCodes.Contains(beneficiary.CountryCode) &&
            (interacEmail
                ? beneficiary.Email != null && beneficiary.Email != "" &&
                  beneficiary.ContactFirstName != null && beneficiary.ContactFirstName != "" &&
                  beneficiary.ContactLastName != null && beneficiary.ContactLastName != ""
                : beneficiary.BankAccounts.Any(bank =>
                !bank.IsDeleted && bank.IsActive &&
                (locallyValidatedForeignBank || bank.ProviderBankId != null && bank.ProviderBankId != "") &&
                bank.CountryCode == beneficiary.CountryCode &&
                bank.CurrencyCode == destinationCurrency));
    }

    public static bool HasReadyDestination(BusinessBeneficiary beneficiary, string destinationCurrency) =>
        destinationCurrency == "CAD"
            ? BusinessBeneficiaryInteracPolicy.IsReady(beneficiary)
            : beneficiary.BankAccounts.Any(bank =>
            bank.CountryCode == beneficiary.CountryCode &&
            bank.CurrencyCode == destinationCurrency &&
            BusinessBeneficiaryPayoutPolicy.IsReady(bank));
}
