using KorridorX.Models.Payments;
using KorridorX.Services.Notifications;

namespace KorridorX.Tests;

public sealed class WalletReceivingAccountNotificationTests
{
    [Fact]
    public void Ready_notice_contains_all_provided_bank_details_in_the_branded_email_and_encodes_provider_text()
    {
        var account = new WalletDepositAccount { Status = "Active", AssetCode = "USD", BankName = "Bank <example>",
            AccountName = "Kay & Company", AccountNumber = "0012345678", Iban = "GB12TEST00001111222233",
            SortCode = "12-34-56", RoutingNumber = "012345678", ProviderReference = "opaque-provider-reference" };
        var message = WalletActivityNotificationFactory.ReceivingAccountReady(account);
        var rendered = BrandedEmailTemplate.Render(message.Subject, message.Body, "https://app.staging.korridorx.com");
        Assert.Contains("Bank &lt;example&gt;", rendered.Html);
        Assert.Contains("Kay &amp; Company", rendered.Html);
        foreach (var value in new[] { account.AccountNumber, account.Iban, account.SortCode, account.RoutingNumber })
            Assert.Contains(value, rendered.PlainText);
        Assert.Contains("Currency", rendered.PlainText);
        Assert.Contains("USD", rendered.PlainText);
        Assert.Contains("If you did not request", rendered.PlainText);
        Assert.Contains("/assets/brand/korridorx-logo-light.png", rendered.Html);
        Assert.DoesNotContain(account.ProviderReference, rendered.Html);
        Assert.DoesNotContain("<example>", rendered.Html);
    }

    [Theory]
    [InlineData("Creating", "12345678")]
    [InlineData("Pending", "12345678")]
    [InlineData("Failed", "12345678")]
    [InlineData("Active", null)]
    public void Unconfirmed_or_incomplete_account_cannot_generate_a_ready_notice(string status, string? number)
    {
        Assert.Throws<InvalidOperationException>(() => WalletActivityNotificationFactory.ReceivingAccountReady(
            new WalletDepositAccount { Status = status, AssetCode = "NGN", AccountNumber = number }));
    }

    [Fact]
    public void Optional_fields_are_omitted_and_iban_only_accounts_are_supported()
    {
        var message = WalletActivityNotificationFactory.ReceivingAccountReady(new WalletDepositAccount
            { Status = "Active", AssetCode = "EUR", Iban = "DE89370400440532013000" });
        Assert.Contains("IBAN", message.Body);
        Assert.Contains("DE89370400440532013000", message.Body);
        Assert.DoesNotContain("Account number:", message.Body);
        Assert.DoesNotContain("Sort code:", message.Body);
        Assert.DoesNotContain("Routing number:", message.Body);
    }
}
