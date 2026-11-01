using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Tests;

public sealed class BusinessTransferRecipientEligibilityPolicyTests
{
    [Fact]
    public void Only_same_business_verified_matching_currency_and_enabled_country_is_listed()
    {
        var business = Guid.NewGuid();
        var otherBusiness = Guid.NewGuid();
        BusinessBeneficiary Recipient(string country, string bankCurrency, bool verified = true,
            bool active = true, Guid? owner = null, BusinessBeneficiaryType type = BusinessBeneficiaryType.Individual)
        {
            return new BusinessBeneficiary
            {
                BusinessProfileId = owner ?? business,
                CountryCode = country,
                BeneficiaryType = type,
                IsActive = active,
                BankAccounts = [new BusinessBeneficiaryBankAccount
                {
                    CountryCode = country,
                    CurrencyCode = bankCurrency,
                    IsActive = true,
                    IsVerified = verified
                }]
            };
        }

        var valid = Recipient("GB", "GBP");
        var ngnOnly = Recipient("NG", "NGN");
        var unsupportedCountry = Recipient("NG", "GBP");
        var unverified = Recipient("GB", "GBP", verified: false);
        var inactive = Recipient("GB", "GBP", active: false);
        var otherTenant = Recipient("GB", "GBP", owner: otherBusiness);
        var wrongType = Recipient("GB", "GBP", type: BusinessBeneficiaryType.Business);
        var criteria = BusinessTransferRecipientEligibilityPolicy.ForQuote(
            business, BusinessBeneficiaryType.Individual, "GBP", ["GB", "CA"]);

        var matches = new[] { valid, ngnOnly, unsupportedCountry, unverified, inactive, otherTenant, wrongType }
            .AsQueryable().Where(criteria).ToArray();
        Assert.Single(matches);
        Assert.Same(valid, matches[0]);
    }

    [Fact]
    public void Wrong_bank_country_and_inactive_bank_cannot_pass()
    {
        var business = Guid.NewGuid();
        var beneficiary = new BusinessBeneficiary
        {
            BusinessProfileId = business, CountryCode = "GB", IsActive = true,
            BeneficiaryType = BusinessBeneficiaryType.Individual,
            BankAccounts =
            [
                new BusinessBeneficiaryBankAccount { CountryCode = "CA", CurrencyCode = "GBP", IsActive = true, IsVerified = true },
                new BusinessBeneficiaryBankAccount { CountryCode = "GB", CurrencyCode = "GBP", IsActive = false, IsVerified = true }
            ]
        };
        var predicate = BusinessTransferRecipientEligibilityPolicy.ForQuote(
            business, BusinessBeneficiaryType.Individual, "GBP", ["GB"]);
        Assert.False(predicate.Compile()(beneficiary));
    }
}
