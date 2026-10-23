using System;
using System.Threading;
using System.Threading.Tasks;
using KorridorX.Models.Enums;
using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalQuoteGatewayTests
{
    [Fact]
    public async Task Quote_is_routed_to_the_selected_provider_not_a_global_hardcoded_provider()
    {
        var adapter = new FakeProvider("TEST-PROVIDER");
        var gateway = new WalletWithdrawalQuoteGateway(new IWalletWithdrawalQuoteProvider[] { adapter });
        var quote = await gateway.QuoteAsync("test-provider", "NGN", "provider-customer",
            100m, 2, PaymentMethod.BankTransfer);
        Assert.Equal(2m, quote.ProviderFee);
        Assert.Equal(98m, quote.RecipientAmount);
        Assert.Equal(1, adapter.QuoteCalls);
    }

    [Fact]
    public async Task Unknown_provider_never_falls_back_to_blaaiz()
    {
        var adapter = new FakeProvider("BLAAIZ");
        var gateway = new WalletWithdrawalQuoteGateway(new IWalletWithdrawalQuoteProvider[] { adapter });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.QuoteAsync("OTHER", "NGN", "customer", 100m, 2, PaymentMethod.BankTransfer));
        Assert.Equal(0, adapter.QuoteCalls);
    }

    private sealed class FakeProvider(string providerCode) : IWalletWithdrawalQuoteProvider
    {
        public string ProviderCode => providerCode;
        public int QuoteCalls { get; private set; }
        public Task<WalletWithdrawalFeeAmounts> QuoteAsync(string currencyCode, string providerCustomerId,
            decimal walletDebit, int decimalPlaces, PaymentMethod paymentMethod, CancellationToken ct = default)
        {
            QuoteCalls++;
            return Task.FromResult(new WalletWithdrawalFeeAmounts(2m, walletDebit - 2m));
        }
    }
}
