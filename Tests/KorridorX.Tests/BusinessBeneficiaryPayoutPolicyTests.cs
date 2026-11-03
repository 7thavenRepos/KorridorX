using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessBeneficiaries;
using Xunit;

namespace KorridorX.Tests;

public sealed class BusinessBeneficiaryPayoutPolicyTests
{
    [Fact]
    public void Gbp_accepts_optional_bank_name_but_requires_account_sort_and_address()
    {
        var account = new BusinessBeneficiaryBankAccount
        {
            CountryCode = "GB", CurrencyCode = "GBP", AccountName = "Example User",
            AccountNumber = "40944513", SortCode = "20-25-32", BankName = "",
            Street = "1 Example Street", City = "London", PostalCode = "SW1A 1AA"
        };
        BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account);
        Assert.Equal("", account.BankName);
        Assert.Null(account.ProviderBankId);
        Assert.Equal("202532", account.SortCode);
        Assert.False(account.IsVerified);

        // Bank-directory IDs must not be repurposed to certify foreign details.
        account.ProviderBankId = "unrelated-provider-bank";
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account));
        account.ProviderBankId = null;
        account.AccountNumber = "1234567";
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account));
    }

    [Fact]
    public void Eur_uses_iban_and_bic_without_an_account_number()
    {
        var account = NewAccount("DE", "EUR");
        account.AccountName = "Example User";
        account.Iban = "DE89370400440532013000";
        account.SwiftBic = "COBADEFFXXX";
        account.Street = "1 Example Street";
        account.City = "Berlin";
        account.PostalCode = "10115";
        BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account);
        Assert.Equal("", account.AccountNumber);
        Assert.Equal("DE89370400440532013000", account.Iban);
        Assert.False(account.IsVerified);
    }

    [Fact]
    public void Usd_wire_requires_swift_in_addition_to_ach_details()
    {
        var account = NewAccount("US", "USD");
        account.PayoutMethod = PaymentMethod.Wire;
        account.AccountName = "Example User"; account.BankName = "Example Bank";
        account.AccountNumber = "123456789"; account.RoutingNumber = "021000021";
        account.RecipientType = "individual"; account.AccountType = "checking";
        account.State = "NY"; account.Street = "1 Example Street";
        account.City = "New York"; account.PostalCode = "10001";
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account));
        account.SwiftBic = "CHASUS33XXX";
        BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account);
        Assert.Equal(PaymentMethod.Wire, account.PayoutMethod);
    }

    [Fact]
    public void Ngn_manual_bank_without_provider_id_is_an_unverified_draft_only()
    {
        var account = NewAccount("NG", "NGN");
        account.AccountNumber = "0123456789";
        BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account);
        Assert.Null(account.ProviderBankId);
        Assert.False(account.IsVerified);
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.Validate(account));
    }

    [Fact]
    public void Interac_is_not_a_business_bank_account_method()
    {
        var account = NewAccount("CA", "CAD");
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account));
    }

    [Fact]
    public void Currency_must_support_the_actual_recipient_bank_country()
    {
        var account = NewAccount("US", "GBP");
        account.AccountName = "Example User";
        account.AccountNumber = "40944513";
        account.SortCode = "202532";
        account.Street = "1 Example Street";
        account.City = "New York";
        account.PostalCode = "10001";
        Assert.Throws<InvalidOperationException>(() => BusinessBeneficiaryPayoutPolicy.NormalizeDraft(account));
    }

    private static BusinessBeneficiaryBankAccount NewAccount(string country, string currency) =>
        new() { CountryCode = country, CurrencyCode = currency };
}
