using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Payments;

namespace KorridorX.Tests;

public sealed class FundingInstructionsTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("GBP", "sortCode", "04-03-00")]
    [InlineData("EUR", "iban", "DE-test-iban")]
    [InlineData("USD", "routingNumber", "021000021")]
    [InlineData("NGN", "bankCode", "044")]
    public void Ready_accounts_preserve_currency_specific_text_fields(string currency, string key, string value)
    {
        var result = FundingInstructionsFactory.ForBankAccount(currency, true, "0012345678", "Test customer", "Test bank", Details());
        Assert.True(result.IsReady);
        Assert.Contains(result.Fields, x => x.Key == key && x.Value == value && x.Copyable);
        Assert.Contains(result.Fields, x => x.Key == "accountNumber" && x.Value == "0012345678");
        Assert.Equal("bank_transfer", result.Kind); // Currency does not establish ACH/wire capability.
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("NGN")]
    public void Unready_accounts_disclose_no_receiving_details(string currency)
    {
        var result = FundingInstructionsFactory.ForBankAccount(currency, false, "secret-number", "Name", "Bank", Details());
        Assert.False(result.IsReady);
        Assert.Empty(result.Fields);
        Assert.Empty(result.Steps);
        Assert.DoesNotContain("secret-number", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("NGN")]
    [InlineData("XYZ")]
    public void Incomplete_or_unknown_receiving_details_are_not_instructions(string currency)
    {
        var result = FundingInstructionsFactory.ForBankAccount(currency, true, "123", "Name", "Bank", null);
        Assert.False(result.IsReady);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void Interac_request_does_not_invent_receiving_email_or_payment_reference()
    {
        var collection = Interac(CollectionStatus.Initiated);
        collection.Reference = "LOCAL-COLLECTION-ID";
        collection.ProviderReference = "TRANSACTION-REFERENCE";
        var result = FundingInstructionsFactory.ForCollection(collection, Now)!;
        Assert.True(result.IsReady);
        Assert.Equal("interac_money_request", result.Kind);
        Assert.Equal(collection.ProviderExpiresAt, result.ExpiresAt);
        Assert.Empty(result.Fields);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("LOCAL-COLLECTION-ID", json);
        Assert.DoesNotContain("TRANSACTION-REFERENCE", json);
    }

    [Theory]
    [InlineData(CollectionStatus.Pending)]
    [InlineData(CollectionStatus.Successful)]
    [InlineData(CollectionStatus.Failed)]
    [InlineData(CollectionStatus.Cancelled)]
    [InlineData(CollectionStatus.Refunded)]
    [InlineData(CollectionStatus.RefundPending)]
    [InlineData(CollectionStatus.Expired)]
    [InlineData(CollectionStatus.RefundFailed)]
    public void Non_actionable_collections_have_no_payment_steps(CollectionStatus status)
    {
        var result = FundingInstructionsFactory.ForCollection(Interac(status), Now)!;
        Assert.False(result.IsReady);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void Exact_expiry_closes_instructions_without_waiting_for_webhook()
    {
        var collection = Interac(CollectionStatus.Processing);
        collection.ProviderExpiresAt = Now;
        Assert.False(FundingInstructionsFactory.ForCollection(collection, Now)!.IsReady);
    }

    [Fact]
    public void Settled_payment_does_not_become_expired_in_presentation()
    {
        var collection = Interac(CollectionStatus.Successful);
        collection.ProviderExpiresAt = Now.AddHours(-1);
        var result = FundingInstructionsFactory.ForCollection(collection, Now)!;
        Assert.Contains("confirmed", result.Message);
        Assert.Null(result.ExpiresAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://checkout.example.test")]
    [InlineData("javascript:alert(1)")]
    public void Card_without_secure_checkout_is_not_ready(string? url)
    {
        var collection = Interac(CollectionStatus.Initiated);
        collection.PaymentMethod = PaymentMethod.Card;
        collection.CurrencyCode = "USD";
        collection.CheckoutUrl = url;
        Assert.False(FundingInstructionsFactory.ForCollection(collection, Now)!.IsReady);
    }

    [Fact]
    public void USD_provider_routing_number_survives_metadata_projection_without_credentials()
    {
        var state = BlaaizVirtualAccountState.ParseResponse("""
            {"data":{"id":"account-id","status":"SUCCESSFUL","currency":"USD",
            "account_number":"0012345678","account_name":"Name","bank_name":"Bank",
            "routing_number":"021000021","client_secret":"not-for-display"}}
            """, "wallet", assetCode: "USD");
        Assert.Equal("021000021", state.RoutingNumber);
        var details = BlaaizVirtualAccountState.BankDetails(state.Metadata("wallet"));
        Assert.Equal("021000021", details?.RoutingNumber);
        Assert.DoesNotContain("not-for-display", JsonSerializer.Serialize(details));
        Assert.Null(BlaaizVirtualAccountState.BankDetails("{\"Iban\":\"old-account\"}")?.RoutingNumber);
    }

    private static VirtualAccountBankDetails Details() => new("044", "04-03-00", "DE-test-iban", "021000021");
    private static Collection Interac(CollectionStatus status) => new()
    {
        CurrencyCode = "CAD", PaymentMethod = PaymentMethod.Interac,
        Status = status, Amount = 25, ProviderExpiresAt = Now.AddHours(3)
    };
}
