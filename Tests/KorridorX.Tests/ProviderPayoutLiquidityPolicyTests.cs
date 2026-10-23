using KorridorX.Services.Providers;
using Xunit;

namespace KorridorX.Tests;

public sealed class ProviderPayoutLiquidityPolicyTests
{
    [Theory]
    [InlineData("NGN", 50000, 49000)]
    [InlineData("CAD", 100, 100)]
    public void Verified_matching_provider_wallet_can_cover_the_recipient_amount(
        string currency, int available, int requested)
    {
        var wallet = new ProviderPayoutWalletBalance("provider-wallet", currency, available, true);
        ProviderPayoutLiquidityPolicy.EnsureSufficient(wallet, "provider-wallet", currency, requested);
    }

    [Theory]
    [InlineData("NGN", 100, 101)]
    [InlineData("CAD", 0, 10)]
    [InlineData("GBP", -1, 5)]
    public void Insufficient_liquidity_blocks_outbound_payout(
        string currency, int available, int requested)
    {
        var wallet = new ProviderPayoutWalletBalance("provider-wallet", currency, available, true);
        Assert.Throws<InvalidOperationException>(() =>
            ProviderPayoutLiquidityPolicy.EnsureSufficient(wallet, "provider-wallet", currency, requested));
    }

    [Fact]
    public void Different_currency_cannot_be_treated_as_destination_liquidity()
    {
        var gbp = new ProviderPayoutWalletBalance("wallet-gbp", "GBP", 100000m, true);
        Assert.Throws<InvalidOperationException>(() =>
            ProviderPayoutLiquidityPolicy.EnsureSufficient(gbp, "wallet-gbp", "NGN", 10000m));
    }

    [Fact]
    public void Mismatched_or_inactive_provider_wallet_is_never_accepted()
    {
        var ngn = new ProviderPayoutWalletBalance("wallet-1", "NGN", 100000m, false);
        Assert.Throws<InvalidOperationException>(() =>
            ProviderPayoutLiquidityPolicy.EnsureSufficient(ngn, "wallet-1", "NGN", 100m));
        Assert.Throws<InvalidOperationException>(() =>
            ProviderPayoutLiquidityPolicy.EnsureSufficient(ngn with { IsActive = true }, "wallet-2", "NGN", 100m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Zero_or_negative_required_amount_is_rejected(int amount)
    {
        var ngn = new ProviderPayoutWalletBalance("wallet", "NGN", 100m, true);
        Assert.Throws<InvalidOperationException>(() =>
            ProviderPayoutLiquidityPolicy.EnsureSufficient(ngn, "wallet", "NGN", amount));
    }
}
