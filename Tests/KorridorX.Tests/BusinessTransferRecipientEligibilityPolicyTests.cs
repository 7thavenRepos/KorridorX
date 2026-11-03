using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessBeneficiaries;
using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Tests;

public sealed class BusinessTransferRecipientEligibilityPolicyTests
{
    [Fact]
    public void Only_same_business_ready_matching_currency_and_enabled_country_is_listed()
    {
        var business = Guid.NewGuid();
        var otherBusiness = Guid.NewGuid();
        BusinessBeneficiary Recipient(string country, string bankCurrency, bool validDetails = true,
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
                    IsVerified = false,
                    AccountName = "Example User",
                    AccountNumber = validDetails ? "40944513" : "123",
                    SortCode = "202532",
                    Street = "1 Example Street", City = "London", PostalCode = "SW1A 1AA"
                }]
            };
        }

        var valid = Recipient("GB", "GBP");
        var ngnOnly = Recipient("NG", "NGN");
        var unsupportedCountry = Recipient("NG", "GBP");
        var badBankDetails = Recipient("GB", "GBP", validDetails: false);
        var inactive = Recipient("GB", "GBP", active: false);
        var otherTenant = Recipient("GB", "GBP", owner: otherBusiness);
        var wrongType = Recipient("GB", "GBP", type: BusinessBeneficiaryType.Business);
        var criteria = BusinessTransferRecipientEligibilityPolicy.ForQuote(
            business, BusinessBeneficiaryType.Individual, "GBP", ["GB", "CA"]);

        var matches = new[] { valid, ngnOnly, unsupportedCountry, badBankDetails, inactive, otherTenant, wrongType }
            .AsQueryable().Where(criteria)
            .Where(x => BusinessTransferRecipientEligibilityPolicy.HasReadyDestination(x, "GBP"))
            .ToArray();
        Assert.Single(matches);
        Assert.Same(valid, matches[0]);
        Assert.False(valid.BankAccounts.Single().IsVerified);
        Assert.True(BusinessBeneficiaryPayoutPolicy.IsReady(valid.BankAccounts.Single()));
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

    [Fact]
    public void Ngn_selected_directory_bank_is_eligible_without_provider_name_lookup()
    {
        var business = Guid.NewGuid();
        var beneficiary = new BusinessBeneficiary
        {
            BusinessProfileId = business, CountryCode = "NG", IsActive = true,
            BeneficiaryType = BusinessBeneficiaryType.Individual,
            BankAccounts = [new BusinessBeneficiaryBankAccount
            {
                CountryCode = "NG", CurrencyCode = "NGN", IsActive = true,
                ProviderBankId = "bank-123", AccountNumber = "0123456789", IsVerified = false
            }]
        };
        var filter = BusinessTransferRecipientEligibilityPolicy.ForQuote(
            business, BusinessBeneficiaryType.Individual, "NGN", ["NG"]);
        Assert.True(filter.Compile()(beneficiary));
        Assert.True(BusinessTransferRecipientEligibilityPolicy.HasReadyDestination(beneficiary, "NGN"));
        Assert.False(beneficiary.BankAccounts.Single().IsVerified);
        beneficiary.BankAccounts.Single().ProviderBankId = null;
        Assert.False(filter.Compile()(beneficiary));
        Assert.False(BusinessTransferRecipientEligibilityPolicy.HasReadyDestination(beneficiary, "NGN"));
    }
}
