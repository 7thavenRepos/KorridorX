using KorridorX.Dtos.Wallets;
using KorridorX.Models.Fx;
using KorridorX.Services.Wallets;

namespace KorridorX.Tests;

public sealed class ConsumerWalletValuationTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Uses_customer_rates_and_available_balances_without_including_holds()
    {
        var wallets = new[] { Wallet("CAD", 10m, held: 70m), Wallet("GBP", 2m, held: 40m) };
        var rate = Rate("GBP", "CAD", 1.25m);
        rate.ProviderRate = 50m;
        var result = Calculate(wallets, new[] { rate });
        Assert.Equal("Complete", result.Status);
        Assert.Equal(12.50m, result.EstimatedAvailableBalance);
        Assert.Equal("12.50", result.EstimatedAvailableBalanceText);
        Assert.Equal(2.50m, result.Components[1].EstimatedBaseAmount);
        Assert.Equal(rate.Id, result.Components[1].ExchangeRateId);
        Assert.Equal(Now.AddMinutes(55), result.ValidUntil);
        Assert.Equal(40m, wallets[1].HeldBalance);
        Assert.Equal(2m, wallets[1].AvailableBalance);
        Assert.True(rate.IsActive);
    }

    [Fact]
    public void Missing_one_rate_withholds_the_total_instead_of_returning_a_partial_sum()
    {
        var result = Calculate(new[] { Wallet("CAD", 10), Wallet("GBP", 2), Wallet("JPY", 3) },
            new[] { Rate("GBP", "CAD", 1.25m) });
        Assert.Equal("Unavailable", result.Status);
        Assert.Null(result.EstimatedAvailableBalance);
        Assert.Null(result.EstimatedAvailableBalanceText);
        Assert.Null(result.ValidUntil);
        Assert.Equal("MissingRate", result.Components[2].Status);
        Assert.Equal(2.50m, result.Components[1].EstimatedBaseAmount);
    }

    [Fact]
    public void Reverse_and_cross_rates_do_not_create_an_unconfigured_direct_rate()
    {
        var result = Calculate(new[] { Wallet("CAD", 1), Wallet("GBP", 1) }, new[]
        {
            Rate("CAD", "GBP", 2), Rate("GBP", "USD", 1), Rate("USD", "CAD", 2)
        });
        Assert.Null(result.EstimatedAvailableBalance);
        Assert.Equal("MissingRate", result.Components[1].Status);
    }

    [Fact]
    public void Future_expired_inactive_and_deleted_rates_are_not_usable()
    {
        var future = Rate("GBP", "CAD", 2); future.EffectiveFrom = Now.AddMinutes(1);
        var expired = Rate("GBP", "CAD", 3); expired.EffectiveTo = Now;
        var inactive = Rate("GBP", "CAD", 4); inactive.IsActive = false;
        var deleted = Rate("GBP", "CAD", 5); deleted.IsDeleted = true;
        var result = Calculate(new[] { Wallet("CAD", 1), Wallet("GBP", 1) },
            new[] { future, expired, inactive, deleted });
        Assert.Equal("MissingRate", result.Components[1].Status);
        Assert.Null(result.EstimatedAvailableBalance);
    }

    [Fact]
    public void Configured_freshness_and_its_exact_expiry_are_enforced()
    {
        var rate = Rate("GBP", "CAD", 2);
        rate.EffectiveFrom = Now.AddMinutes(-10);
        var wallets = new[] { Wallet("CAD", 1), Wallet("GBP", 1) };
        var stale = Calculate(wallets, new[] { rate }, staleMinutes: 10);
        Assert.Equal("StaleRate", stale.Components[1].Status);
        Assert.Null(stale.EstimatedAvailableBalance);
        var fresh = Calculate(wallets, new[] { rate }, staleMinutes: 60);
        Assert.Equal(3m, fresh.EstimatedAvailableBalance);
        Assert.Equal(Now.AddMinutes(50), fresh.ValidUntil);
    }

    [Fact]
    public void The_earliest_rate_expiry_limits_the_combined_estimate()
    {
        var gbp = Rate("GBP", "CAD", 2); gbp.EffectiveTo = Now.AddMinutes(3);
        var jpy = Rate("JPY", "CAD", 1); jpy.EffectiveFrom = Now.AddMinutes(-58);
        var result = Calculate(new[] { Wallet("CAD", 1), Wallet("GBP", 1), Wallet("JPY", 1) },
            new[] { gbp, jpy });
        Assert.Equal(4m, result.EstimatedAvailableBalance);
        Assert.Equal(Now.AddMinutes(2), result.ValidUntil);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_nonpositive_customer_rate_does_not_look_like_a_zero_balance(int value)
    {
        var result = Calculate(new[] { Wallet("CAD", 1), Wallet("GBP", 1) },
            new[] { Rate("GBP", "CAD", value) });
        Assert.Equal("InvalidRate", result.Components[1].Status);
        Assert.Null(result.EstimatedAvailableBalance);
    }

    [Fact]
    public void An_invalid_latest_effective_rate_does_not_fall_back_to_an_older_price()
    {
        var older = Rate("GBP", "CAD", 2); older.EffectiveFrom = Now.AddMinutes(-10);
        var current = Rate("GBP", "CAD", 0);
        var result = Calculate(new[] { Wallet("CAD", 1), Wallet("GBP", 1) }, new[] { older, current });
        Assert.Equal(current.Id, result.Components[1].ExchangeRateId);
        Assert.Null(result.EstimatedAvailableBalance);
    }

    [Fact]
    public void Zero_balances_need_no_conversion_rate()
    {
        var result = Calculate(new[] { Wallet("CAD", 10), Wallet("GBP", 0) }, Array.Empty<ExchangeRate>());
        Assert.Equal(10m, result.EstimatedAvailableBalance);
        Assert.Equal("ZeroBalance", result.Components[1].Status);
        Assert.Null(result.Components[1].CustomerRate);
        Assert.Null(result.ValidUntil);
    }

    [Fact]
    public void Unconfigured_nonzero_assets_do_not_disappear_from_the_total()
    {
        var wallets = new[] { Wallet("CAD", 10), Wallet("GBP", 1) };
        var result = ConsumerWalletValuationCalculator.Calculate(wallets,
            new[] { Asset("CAD") }, new[] { Rate("GBP", "CAD", 2) }, "CAD", 60, Now);
        Assert.Equal("UnsupportedAsset", result.Components[1].Status);
        Assert.Null(result.EstimatedAvailableBalance);
    }

    [Fact]
    public void A_frozen_wallet_is_not_reported_as_available_money()
    {
        var wallets = new[] { Wallet("CAD", 10), Wallet("GBP", 1, status: "Frozen") };
        var result = Calculate(wallets, new[] { Rate("GBP", "CAD", 2) });
        Assert.Equal("InactiveWallet", result.Components[1].Status);
        Assert.Null(result.EstimatedAvailableBalance);
    }

    [Fact]
    public void Display_currency_must_be_owned_and_master_data_eligible()
    {
        var wallets = new[] { Wallet("CAD", 10) };
        Assert.Throws<InvalidOperationException>(() => ConsumerWalletValuationCalculator.Calculate(
            wallets, new[] { Asset("CAD"), Asset("GBP") }, Array.Empty<ExchangeRate>(), "GBP", 60, Now));
        Assert.Throws<InvalidOperationException>(() => ConsumerWalletValuationCalculator.Calculate(
            wallets, Array.Empty<AvailableConsumerWalletAssetDto>(), Array.Empty<ExchangeRate>(), "CAD", 60, Now));
        Assert.Throws<InvalidOperationException>(() => Calculate(wallets, Array.Empty<ExchangeRate>(), staleMinutes: 0));
    }

    [Fact]
    public void Currency_code_normalization_does_not_require_free_text_new_currencies()
    {
        var result = ConsumerWalletValuationCalculator.Calculate(new[] { Wallet("CAD", 10) },
            new[] { Asset("CAD") }, Array.Empty<ExchangeRate>(), " cad ", 60, Now);
        Assert.Equal("CAD", result.BaseAssetCode);
        Assert.Equal(10m, result.EstimatedAvailableBalance);
    }

    [Fact]
    public void Currency_precision_rounds_each_component_and_the_total_matches_the_breakdown()
    {
        var wallets = new[] { Wallet("JPY", 0), Wallet("CAD", 1), Wallet("GBP", 1) };
        var assets = new[] { Asset("JPY", 0), Asset("CAD"), Asset("GBP") };
        var result = ConsumerWalletValuationCalculator.Calculate(wallets, assets,
            new[] { Rate("CAD", "JPY", 1.5m), Rate("GBP", "JPY", 2.5m) }, "JPY", 60, Now);
        Assert.Equal(2m, result.Components[1].EstimatedBaseAmount);
        Assert.Equal(2m, result.Components[2].EstimatedBaseAmount);
        Assert.Equal(4m, result.EstimatedAvailableBalance);
        Assert.Equal("4", result.EstimatedAvailableBalanceText);
    }

    [Fact]
    public void Three_decimal_currency_precision_is_preserved_in_the_display_text()
    {
        var wallets = new[] { Wallet("KWD", 1), Wallet("CAD", 1) };
        var result = ConsumerWalletValuationCalculator.Calculate(wallets,
            new[] { Asset("KWD", 3), Asset("CAD") }, new[] { Rate("CAD", "KWD", 0.1235m) }, "KWD", 60, Now);
        Assert.Equal(1.124m, result.EstimatedAvailableBalance);
        Assert.Equal("1.124", result.EstimatedAvailableBalanceText);
    }

    [Fact]
    public void Native_negative_available_balances_are_not_discarded()
    {
        var result = Calculate(new[] { Wallet("CAD", -2), Wallet("GBP", 1) }, new[] { Rate("GBP", "CAD", 1) });
        Assert.Equal(-1m, result.EstimatedAvailableBalance);
    }

    [Fact]
    public void Component_and_total_overflow_withhold_the_estimate()
    {
        var wallets = new[] { Wallet("CAD", decimal.MaxValue), Wallet("GBP", decimal.MaxValue) };
        var convertedOverflow = Calculate(wallets, new[] { Rate("GBP", "CAD", 2) });
        Assert.Equal("AmountOverflow", convertedOverflow.Components[1].Status);
        Assert.Null(convertedOverflow.EstimatedAvailableBalance);
        var sumOverflow = Calculate(wallets, new[] { Rate("GBP", "CAD", 1) });
        Assert.Equal("Unavailable", sumOverflow.Status);
        Assert.Null(sumOverflow.EstimatedAvailableBalance);
    }

    private static ConsumerWalletValuationDto Calculate(
        ConsumerWalletDto[] wallets, ExchangeRate[] rates, int staleMinutes = 60) =>
        ConsumerWalletValuationCalculator.Calculate(wallets,
            wallets.Select(x => Asset(x.AssetCode)).ToArray(), rates, "CAD", staleMinutes, Now);

    private static ConsumerWalletDto Wallet(string code, decimal available, decimal held = 0, string status = "Active") =>
        new(Guid.NewGuid(), code, code, code, 2, status, available + held, available, held,
            code == "CAD", true, true, true, true, false, false, Now);

    private static AvailableConsumerWalletAssetDto Asset(string code, int precision = 2) =>
        new(code, code, code, precision, code == "CAD", true, true, true, true, false, false, true);

    private static ExchangeRate Rate(string source, string destination, decimal customerRate) => new()
    {
        SourceCurrencyCode = source, DestinationCurrencyCode = destination,
        ProviderRate = customerRate, CustomerRate = customerRate,
        EffectiveFrom = Now.AddMinutes(-5), CreatedAt = Now.AddMinutes(-5), IsActive = true
    };
}
