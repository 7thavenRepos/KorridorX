using KorridorX.Models.Enums;
using KorridorX.Models.Recipients;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Recipients;

namespace KorridorX.Tests;

public sealed class BlaaizCurrencyRequirementsTests
{
    [Theory]
    [InlineData("US", "USD")]
    [InlineData("GB", "GBP")]
    [InlineData("DE", "EUR")]
    [InlineData("GB", "EUR")]
    public void ImplementedForeignBankRoutesAreRecipientRoutes(string country, string currency) =>
        Assert.Equal(RecipientDeliveryPolicy.BankAccount, RecipientDeliveryPolicy.Resolve(country, currency));

    [Theory]
    [InlineData("EU", "EUR")]
    [InlineData("NG", "EUR")]
    [InlineData("CA", "USD")]
    [InlineData("US", "GBP")]
    public void AVisibleAssetCannotInventABankDestination(string country, string currency) =>
        Assert.Null(RecipientDeliveryPolicy.Resolve(country, currency));

    [Fact]
    public void LocalUsdReadinessDoesNotClaimProviderVerification()
    {
        var account = Usd();
        Assert.True(RecipientPayoutPolicy.IsReady(account));
        Assert.False(account.IsVerified);
        Assert.Null(account.VerifiedAt);
        account.State = null;
        Assert.False(RecipientPayoutPolicy.IsReady(account));
    }

    [Fact]
    public void WireCannotReuseAchDetailsWithoutSwift()
    {
        var account = Usd();
        account.PayoutMethod = PaymentMethod.Wire;
        Assert.False(RecipientPayoutPolicy.IsReady(account));
        account.SwiftBic = "BOFAUS3N";
        Assert.True(RecipientPayoutPolicy.IsReady(account));
    }

    [Fact]
    public void EurRequiresValidIbanAndMatchingConfiguredBankCountry()
    {
        var account = new RecipientBankAccount { IsActive = true, CountryCode = "DE", CurrencyCode = "EUR",
            AccountName = "Test Recipient", Iban = "DE89 3704 0044 0532 0130 00", SwiftBic = "COBADEFFXXX",
            Street = "Test Street 1", City = "Berlin", PostalCode = "10115" };
        Assert.True(RecipientPayoutPolicy.IsReady(account));
        RecipientPayoutPolicy.Normalize(account);
        Assert.Equal("DE89370400440532013000", account.Iban);
        account.CountryCode = "FR";
        Assert.False(RecipientPayoutPolicy.IsReady(account));
        account.CountryCode = "DE"; account.Iban = "DE00370400440532013000";
        Assert.False(RecipientPayoutPolicy.IsReady(account));
    }

    [Fact]
    public void ForeignPayoutCarriesRoutingTypeAndAddressWithoutNgnBankId()
    {
        var request = new RemittancePayoutRequest(null, Guid.NewGuid(), PaymentMethod.Ach, 100m,
            "GBP", "USD", "customer", "wallet", "Test", "Recipient", null, null,
            null, "Test Bank", "Test Recipient", "123456789", "026009593", null, null, null, "Test",
            RecipientType: "individual", AccountType: "checking", RecipientCountry: "US",
            RecipientState: "NY", RecipientStreet: "Test Street 1", RecipientCity: "New York",
            RecipientPostalCode: "10001");
        var payload = new BlaaizPayoutRequest();
        BlaaizCurrencyRequirements.ApplyPayoutFields(request, payload);
        Assert.Equal("026009593", payload.RoutingNumber);
        Assert.Equal("checking", payload.AccountType);
        Assert.Equal("individual", payload.RecipientType);
        Assert.Equal("Test Street 1", payload.Street);
        Assert.Null(payload.BankId);
        Assert.Null(payload.SwiftCode);
        Assert.Throws<InvalidOperationException>(() =>
            BlaaizCurrencyRequirements.ApplyPayoutFields(request with { RecipientState = null }, new()));
    }

    private static RecipientBankAccount Usd() => new()
    {
        IsActive = true, CountryCode = "US", CurrencyCode = "USD", PayoutMethod = PaymentMethod.Ach,
        AccountName = "Test Recipient", AccountNumber = "123456789", BankName = "Test Bank",
        RoutingNumber = "026009593", RecipientType = "individual", AccountType = "checking",
        Street = "Test Street 1", City = "New York", State = "NY", PostalCode = "10001"
    };
}
