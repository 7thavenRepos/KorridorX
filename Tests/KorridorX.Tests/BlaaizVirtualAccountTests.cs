using System.Text.Json;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Tests;

public sealed class BlaaizVirtualAccountTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Pending_response_has_no_usable_bank_account(string currency)
    {
        var state = BlaaizVirtualAccountState.ParseResponse(
            JsonSerializer.Serialize(new { data = new { id = "va", status = "PENDING", currency } }), "wallet", "customer", currency);
        var mapping = Mapping(currency);
        state.Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Pending, mapping.Status);
        Assert.Equal(CollectionAccountStatus.Pending, mapping.CollectionAccount.Status);
        Assert.Equal("va", mapping.ProviderAccountId);
        Assert.Null(mapping.AccountNumber);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Ready_event_supplies_bank_details_and_activates_once(string currency)
    {
        var state = BlaaizVirtualAccountState.ParseWebhook(Payload(currency), "virtual_account.ready");
        var mapping = Mapping(currency);
        state.Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Active, mapping.Status);
        Assert.Equal(CollectionAccountStatus.Active, mapping.CollectionAccount.Status);
        Assert.Equal("12345678", mapping.AccountNumber);
        Assert.Equal("GB12EXAMPLE12345678", BlaaizVirtualAccountState.BankDetails(mapping.MetadataJson)?.Iban);
        var updated = mapping.LastUpdatedAt;
        state.Apply(mapping, "wallet");
        Assert.Equal(updated, mapping.LastUpdatedAt);
    }

    [Theory]
    [InlineData(CollectionAccountStatus.Suspended, BusinessCustomerStatus.Active)]
    [InlineData(CollectionAccountStatus.Closed, BusinessCustomerStatus.Active)]
    [InlineData(CollectionAccountStatus.Pending, BusinessCustomerStatus.Suspended)]
    [InlineData(CollectionAccountStatus.Pending, BusinessCustomerStatus.Closed)]
    public void Provider_ready_preserves_local_customer_and_account_restrictions(CollectionAccountStatus status, BusinessCustomerStatus customer)
    {
        var mapping = Mapping("EUR");
        mapping.CollectionAccount.Status = status;
        mapping.CollectionAccount.BusinessCustomer.Status = customer;
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR"), "virtual_account.ready").Apply(mapping, "wallet");
        Assert.Equal(status, mapping.CollectionAccount.Status);
        Assert.Equal(ProviderAccountMappingStatus.Active, mapping.Status);
    }

    [Theory]
    [InlineData("virtual_account.created", "PENDING")]
    [InlineData("virtual_account.failed", "FAILED")]
    [InlineData("virtual_account.rejected", "REJECTED")]
    public void Late_events_cannot_downgrade_ready_account(string eventType, string status)
    {
        var mapping = Mapping("EUR");
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR"), "virtual_account.ready").Apply(mapping, "wallet");
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR", eventType, status), eventType).Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Active, mapping.Status);
        Assert.Equal(CollectionAccountStatus.Active, mapping.CollectionAccount.Status);
    }

    [Theory]
    [InlineData("virtual_account.failed", "FAILED")]
    [InlineData("virtual_account.rejected", "REJECTED")]
    public void Failure_remains_pending_locally_and_requires_deliberate_retry(string eventType, string status)
    {
        var mapping = Mapping("EUR");
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR", eventType, status), eventType).Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Failed, mapping.Status);
        Assert.Equal(CollectionAccountStatus.Pending, mapping.CollectionAccount.Status);
        Assert.Equal("Please review customer verification.", mapping.FailureReason);
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR"), "virtual_account.ready").Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Failed, mapping.Status);
    }

    [Theory]
    [InlineData("wallet_id", "other")]
    [InlineData("customer_id", "other")]
    [InlineData("currency", "GBP")]
    public void Response_identity_conflicts_are_rejected(string field, string value)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(Payload("EUR"))!;
        payload[field] = value;
        Assert.Throws<InvalidOperationException>(() => BlaaizVirtualAccountState.ParseResponse(
            JsonSerializer.Serialize(payload), "wallet", "customer", "EUR"));
    }

    [Theory]
    [InlineData("wallet_id")]
    [InlineData("customer_id")]
    [InlineData("currency")]
    [InlineData("virtual_account_id")]
    [InlineData("status")]
    [InlineData("iban")]
    public void Incomplete_ready_event_is_rejected(string field)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(Payload("EUR"))!;
        payload.Remove(field);
        Assert.Throws<InvalidOperationException>(() => BlaaizVirtualAccountState.ParseWebhook(JsonSerializer.Serialize(payload), "virtual_account.ready"));
    }

    [Fact]
    public void Ready_GBP_requires_sort_code()
    {
        Assert.Throws<InvalidOperationException>(() => BlaaizVirtualAccountState.ParseWebhook(
            Payload("GBP").Replace("\"sort_code\":\"04-03-00\"", "\"sort_code\":null"), "virtual_account.ready"));
    }

    [Theory]
    [InlineData("virtual_account.ready", "PENDING")]
    [InlineData("virtual_account.rejected", "SUCCESSFUL")]
    [InlineData("virtual_account.unknown", "PENDING")]
    public void Conflicting_or_unknown_event_cannot_change_state(string eventType, string status) =>
        Assert.Throws<InvalidOperationException>(() => BlaaizVirtualAccountState.ParseWebhook(Payload("EUR", eventType, status), eventType));

    [Fact]
    public void Ready_event_cannot_reenable_an_administratively_disabled_mapping()
    {
        var mapping = Mapping("EUR");
        mapping.Status = ProviderAccountMappingStatus.Disabled;
        BlaaizVirtualAccountState.ParseWebhook(Payload("EUR"), "virtual_account.ready").Apply(mapping, "wallet");
        Assert.Equal(ProviderAccountMappingStatus.Disabled, mapping.Status);
        Assert.Equal(CollectionAccountStatus.Pending, mapping.CollectionAccount.Status);
    }

    [Fact]
    public void Account_ID_conflict_is_rejected_even_for_terminal_mapping()
    {
        var mapping = Mapping("EUR");
        mapping.ProviderAccountId = "other";
        mapping.Status = ProviderAccountMappingStatus.Active;
        Assert.Throws<InvalidOperationException>(() => BlaaizVirtualAccountState.ParseWebhook(Payload("EUR"), "virtual_account.ready").Apply(mapping, "wallet"));
    }

    [Fact]
    public void Bank_details_projection_excludes_credentials_and_raw_responses()
    {
        var metadata = "{\"Iban\":\"GB-test\",\"SortCode\":\"04-03-00\",\"rawResponse\":\"secret\",\"client_secret\":\"secret\"}";
        var json = JsonSerializer.Serialize(BlaaizVirtualAccountState.BankDetails(metadata));
        Assert.Contains("GB-test", json);
        Assert.DoesNotContain("secret", json);
        Assert.Null(BlaaizVirtualAccountState.BankDetails("invalid"));
    }

    internal static string Payload(string currency, string eventType = "virtual_account.ready", string status = "SUCCESSFUL",
        string id = "va", string customer = "customer", string wallet = "wallet") => JsonSerializer.Serialize(new
        {
            @event = eventType, virtual_account_id = id, customer_id = customer, wallet_id = wallet,
            currency, status, account_number = "12345678", account_name = "Example customer", bank_name = "Example Bank",
            iban = "GB12EXAMPLE12345678", sort_code = "04-03-00", user_reason = "Please review customer verification."
        });

    private static ProviderAccountMapping Mapping(string currency) => new()
    {
        ProviderCustomerId = "customer", Status = ProviderAccountMappingStatus.Pending,
        CollectionAccount = new CollectionAccount
        {
            AssetCode = currency, Status = CollectionAccountStatus.Pending,
            BusinessCustomer = new BusinessCustomer { Status = BusinessCustomerStatus.Active }
        }
    };
}
