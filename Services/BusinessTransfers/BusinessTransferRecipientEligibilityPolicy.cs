using System.Linq.Expressions;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;

namespace KorridorX.Services.BusinessTransfers;

/// <summary>
/// Apply exactly the same business, transfer type, receiving-country and
/// verified bank-currency restrictions in the database query and UI discovery.
/// This is an eligibility prefilter; payout still validates provider mappings.
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
        return beneficiary =>
            !beneficiary.IsDeleted && beneficiary.IsActive &&
            beneficiary.BusinessProfileId == businessProfileId &&
            beneficiary.BeneficiaryType == expectedType &&
            receivingCountryCodes.Contains(beneficiary.CountryCode) &&
            beneficiary.BankAccounts.Any(bank =>
                !bank.IsDeleted && bank.IsActive && bank.IsVerified &&
                bank.CountryCode == beneficiary.CountryCode &&
                bank.CurrencyCode == destinationCurrency);
    }
}
