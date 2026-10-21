using System.Globalization;
using System.Text.Json;
using KorridorX.Dtos.Wallets;
using KorridorX.Exceptions;
using KorridorX.Models.Fx;
using KorridorX.Services.Wallets;
using KorridorX.Providers.Fx;
using KorridorX.Services.Fx;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizRateDirectionNormalizationTests
{
    [Theory]
    [InlineData("CAD", "1040.00", "0.00096154")]
    [InlineData("USD", "1540.00", "0.00064935")]
    [InlineData("GBP", "1950.00", "0.00051282")]
    [InlineData("EUR", "1800.00", "0.00055556")]
    public async Task Ngn_to_major_fiat_normalizes_ngn_per_foreign_unit_before_markup(
        string target, string rawPrice, string normalizedPrice)
    {
        using var handler = new FxProviderTestSupport.Handler(
            FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(
                value: JsonSerializer.Serialize(rawPrice), pair: "NGN/" + target)));
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);

        var observed = await provider.GetRateAsync("NGN", target);
        var expected = decimal.Parse(normalizedPrice, CultureInfo.InvariantCulture);
        Assert.Equal(expected, observed.Rate);
        Assert.Equal("NGN", observed.SourceCurrencyCode);
        Assert.Equal(target, observed.DestinationCurrencyCode);
        Assert.Contains(rawPrice, audit.LastResponse); // Raw provider evidence is retained.
        Assert.Single(handler.Requests);
        Assert.Equal(1, audit.Completed);
        Assert.Equal(0, audit.Failed);
    }

    [Fact]
    public async Task Live_NGN_to_CAD_amount_applies_markup_after_normalization()
    {
        using var handler = new FxProviderTestSupport.Handler(
            FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(
                value: "1040", pair: "NGN/CAD")));
        using var http = new HttpClient(handler);
        var provider = FxProviderTestSupport.Provider(http,
            new FxProviderTestSupport.Tokens(), new FxProviderTestSupport.Audit());
        var observed = await provider.GetRateAsync("NGN", "CAD");
        var customerRate = FxPricing.Calculate(observed.Rate, 0.1m, null, null);

        Assert.Equal(0.00096154m, observed.Rate);
        Assert.Equal(0.00096058m, customerRate);
        Assert.Equal(1.92m, decimal.Round(2000m * customerRate, 2, MidpointRounding.ToEven));
        Assert.Equal(10.38m, decimal.Round(8.46m + 2000m * customerRate, 2, MidpointRounding.ToEven));
    }

    [Fact]
    public async Task CAD_to_NGN_still_uses_the_direct_multiplier()
    {
        using var handler = new FxProviderTestSupport.Handler(
            FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(
                value: "953.00", pair: "CAD/NGN")));
        using var http = new HttpClient(handler);
        var provider = FxProviderTestSupport.Provider(http,
            new FxProviderTestSupport.Tokens(), new FxProviderTestSupport.Audit());
        var observed = await provider.GetRateAsync("CAD", "NGN");

        Assert.Equal(953.00m, observed.Rate);
        var customerRate = FxPricing.Calculate(observed.Rate, 0.1m, null, null);
        Assert.Equal(952.047m, customerRate);
        Assert.Equal(10054.32m,
            decimal.Round(2000m + 8.46m * customerRate, 2, MidpointRounding.ToEven));
    }

    [Fact]
    public void Customer_wallet_valuation_uses_normalized_ngn_to_cad_multiplier()
    {
        var now = DateTime.UtcNow;
        var wallets = new[]
        {
            Wallet("CAD", 8.46m), Wallet("NGN", 2000m)
        };
        var assets = wallets.Select(x => new AvailableConsumerWalletAssetDto(
            x.AssetCode, x.AssetName, x.Symbol, x.DecimalPlaces, x.IsDefault,
            x.CanDeposit, x.CanWithdraw, x.CanSend, x.CanReceive, x.CanTrade,
            x.CanUseInstant, true)).ToArray();
        var rates = new[]
        {
            new ExchangeRate
            {
                SourceCurrencyCode = "NGN", DestinationCurrencyCode = "CAD",
                ProviderRate = 0.00096154m, CustomerRate = 0.00096058m,
                EffectiveFrom = now.AddSeconds(-1), IsActive = true
            }
        };
        var valuation = ConsumerWalletValuationCalculator.Calculate(
            wallets, assets, rates, "CAD", 60, now);
        Assert.Equal("Complete", valuation.Status);
        Assert.Equal(10.38m, valuation.EstimatedAvailableBalance);
        Assert.Equal(2, valuation.Components.Count);
    }

    private static ConsumerWalletDto Wallet(string currency, decimal available) =>
        new(Guid.NewGuid(), currency, currency, currency, 2, "Active", available, available, 0m,
            currency == "CAD", true, true, true, true, false, false, DateTime.UtcNow);

    [Fact]
    public async Task NGN_to_CAD_already_small_provider_rate_is_rejected_not_double_inverted()
    {
        using var handler = new FxProviderTestSupport.Handler(
            FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(
                value: "0.00096154", pair: "NGN/CAD")));
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);

        await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("NGN", "CAD"));
        Assert.Equal(0, audit.Completed);
        Assert.Equal(1, audit.Failed);
    }

    [Fact]
    public void Unrelated_cross_rate_remains_unchanged()
    {
        var candidate = new BlaaizSelectedFxRate("USD", "CAD", 1.37m,
            "test-123", null, true, true, null, null);
        Assert.Equal(1.37m, BlaaizFxRateParser.DestinationUnitsPerSource(candidate));
    }
}
