using KorridorX.Configuration;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalAllCurrencyTests
{
    [Theory]
    [InlineData("NGN", PaymentMethod.BankTransfer, "NG")]
    [InlineData("CAD", PaymentMethod.Interac, "CA")]
    [InlineData("USD", PaymentMethod.Ach, "US")]
    [InlineData("USD", PaymentMethod.Wire, "US")]
    [InlineData("GBP", PaymentMethod.BankTransfer, "GB")]
    [InlineData("EUR", PaymentMethod.BankTransfer, "DE")]
    public void All_five_fiat_payout_methods_have_real_destination_countries(
        string currency, PaymentMethod method, string expected)
    {
        Assert.Equal(expected, WalletWithdrawalSetupPolicy.DestinationCountry(currency, method, expected));
    }

    [Theory]
    [InlineData("Staging", "https://api-dev.blaaiz.com", true)]
    [InlineData("Staging", "https://api-prod.blaaiz.com", true)]
    [InlineData("Production", "https://api-prod.blaaiz.com", true)]
    [InlineData("Production", "https://api-dev.blaaiz.com", false)]
    [InlineData("Development", "https://api-prod.blaaiz.com", false)]
    [InlineData("Staging", "https://api-prod.blaaiz.com.attacker.test", false)]
    public void Exact_approved_provider_hosts_by_environment(string environment, string url, bool expected)
    {
        var options = new WalletWithdrawalProcessingOptions { Enabled = true, DispatchEnabled = true, RecoveryEnabled = true };
        var provider = new BlaaizOptions { IsEnabled = true, BaseUrl = url };
        Assert.Equal(expected, WalletWithdrawalDispatchSafetyPolicy.CanDispatch(environment, options, provider));
        Assert.Equal(expected, WalletWithdrawalDispatchSafetyPolicy.CanRecover(environment, options, provider));
    }

    [Theory]
    [InlineData("NGN")]
    [InlineData("CAD")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void All_five_currencies_can_be_dispatched_with_normal_limits(string currency)
    {
        var settings = new WalletWithdrawalProcessingOptions { Enabled = true, DispatchEnabled = true };
        var row = new FiatWalletWithdrawal { CurrencyCode = currency, TotalWalletDebit = 100m };
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.IsWithinExecutionDebitLimit(row, "Production", settings));
    }

    [Fact]
    public void Us_ach_preserves_owned_account_and_routing_fields()
    {
        var details = WalletWithdrawalForeignAccount.Create("USD", PaymentMethod.Ach,
            new SaveWalletWithdrawalDestinationRequestDto
            {
                PaymentMethod = PaymentMethod.Ach, AccountName = "Jane Doe",
                AccountNumber = "123456789", BankName = "Example US Bank", AccountType = "checking",
                RoutingNumber = "021000021", DestinationCountryCode = "US", State = "NY",
                Street = "123 Example Street", City = "New York", ZipCode = "10001"
            }, "Jane Doe", false);
        var restored = WalletWithdrawalForeignAccount.Parse(details.ToProtectedPlaintext(), "USD", PaymentMethod.Ach);
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(Row("USD", PaymentMethod.Ach),
            Guid.NewGuid(), "provider-customer", "wallet", restored.ToProtectedPlaintext());
        Assert.Equal("123456789", request.AccountNumber);
        Assert.Equal("021000021", request.RoutingNumber);
        Assert.Equal("checking", request.AccountType);
        Assert.Equal("individual", request.RecipientType);
        Assert.Equal("US", request.RecipientCountry);
        Assert.Equal(100m, request.FromAmount);
    }

    [Fact]
    public void Us_wire_requires_swift()
    {
        var row = new SaveWalletWithdrawalDestinationRequestDto
        {
            PaymentMethod = PaymentMethod.Wire, AccountName = "Jane Doe",
            AccountNumber = "123456789", BankName = "Example US Bank", AccountType = "savings",
            RoutingNumber = "021000021", DestinationCountryCode = "US", State = "NY",
            Street = "123 Example Street", City = "New York", ZipCode = "10001"
        };
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalForeignAccount.Create("USD", PaymentMethod.Wire, row, "Jane Doe", false));
        row.SwiftCode = "CHASUS33";
        var account = WalletWithdrawalForeignAccount.Create("USD", PaymentMethod.Wire, row, "Jane Doe", false);
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(Row("USD", PaymentMethod.Wire),
            Guid.NewGuid(), "customer", "wallet", account.ToProtectedPlaintext());
        Assert.Equal("CHASUS33", request.SwiftBic);
    }

    [Fact]
    public void Gbp_sort_code_and_account_number_are_preserved()
    {
        var details = WalletWithdrawalForeignAccount.Create("GBP", PaymentMethod.BankTransfer,
            new SaveWalletWithdrawalDestinationRequestDto
            {
                PaymentMethod = PaymentMethod.BankTransfer, AccountName = "Jane Doe",
                AccountNumber = "12345678", SortCode = "12-34-56", DestinationCountryCode = "GB",
                Street = "1 Example Road", City = "London", ZipCode = "SW1A 1AA"
            }, "Jane Doe", false);
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(Row("GBP", PaymentMethod.BankTransfer),
            Guid.NewGuid(), "customer", "wallet", details.ToProtectedPlaintext());
        Assert.Equal("123456", request.SortCode);
        Assert.Equal("12345678", request.AccountNumber);
    }

    [Fact]
    public void Eur_iban_mod97_and_bic_must_be_valid()
    {
        var dto = new SaveWalletWithdrawalDestinationRequestDto
        {
            PaymentMethod = PaymentMethod.BankTransfer, AccountName = "Jane Doe",
            Iban = "DE89370400440532013000", BicCode = "COBADEFFXXX", DestinationCountryCode = "DE",
            Street = "1 Example Strasse", City = "Berlin", ZipCode = "10115"
        };
        var good = WalletWithdrawalForeignAccount.Create("EUR", PaymentMethod.BankTransfer, dto, "Jane Doe", false);
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(Row("EUR", PaymentMethod.BankTransfer),
            Guid.NewGuid(), "customer", "wallet", good.ToProtectedPlaintext());
        Assert.Equal("DE89370400440532013000", request.Iban);
        Assert.Equal("COBADEFFXXX", request.BicCode);
        dto.Iban = "DE00370400440532013000";
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalForeignAccount.Create("EUR", PaymentMethod.BankTransfer, dto, "Jane Doe", false));
    }

    [Theory]
    [InlineData(PaymentMethod.Ach, "ACH")]
    [InlineData(PaymentMethod.Wire, "WIRE")]
    public void Fee_preview_supports_us_methods_and_reconciles_exact_amounts(PaymentMethod method, string providerMethod)
    {
        var quote = new BlaaizFeeBreakdownResponse
        {
            PayoutFees = [new BlaaizPayoutFeeBreakdown
            {
                Method = providerMethod, YouSend = 100m, RecipientGets = 97m,
                TotalFees = 3m, BaseFee = 1m, MethodFee = 2m,
                NetAmountToSend = 97m, Rate = 1m,
                AmountIsYouSend = true, AmountIsRecipientGets = false
            }]
        };
        var evaluated = WalletWithdrawalFeePolicy.Evaluate(quote, method, 100m, 2);
        Assert.Equal(3m, evaluated.ProviderFee);
        Assert.Equal(97m, evaluated.RecipientAmount);
    }

    private static FiatWalletWithdrawal Row(string currency, PaymentMethod method) => new()
    {
        CurrencyCode = currency, PaymentMethod = method, Reference = "KXWD-example",
        TotalWalletDebit = 100m, WithdrawalAmount = 100m,
        ExpectedRecipientAmount = 97m, ProviderFee = 3m,
        KorridorXFeePolicy = "NOT_CONFIGURED", FinancialReservationId = Guid.NewGuid(),
        ProtectedAccountNumber = "authenticated-ciphertext", VerifiedAccountName = "Jane Doe"
    };
}
