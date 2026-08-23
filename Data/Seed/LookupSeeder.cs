using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Lookups;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Data.Seed;

public static class LookupSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedCountriesAsync(db);
        await SeedAssetsAsync(db);
        await SeedAssetNetworksAsync(db);
        await SeedCountryAssetsAsync(db);
        await SeedFxAsync(db);
    }

    private static async Task SeedCountriesAsync(AppDbContext db)
    {
        var countries = new[]
        {
            new Country { Code = "US", Name = "United States", Iso3Code = "USA", IsSupported = true, IsSendCountry = true, IsReceiveCountry = false },
            new Country { Code = "CA", Name = "Canada", Iso3Code = "CAN", IsSupported = true, IsSendCountry = true, IsReceiveCountry = false },
            new Country { Code = "GB", Name = "United Kingdom", Iso3Code = "GBR", IsSupported = true, IsSendCountry = true, IsReceiveCountry = true },
            new Country { Code = "NG", Name = "Nigeria", Iso3Code = "NGA", IsSupported = true, IsSendCountry = false, IsReceiveCountry = true }
        };

        foreach (var country in countries)
        {
            if (!await db.Countries.AnyAsync(x => x.Code == country.Code))
                db.Countries.Add(country);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAssetsAsync(AppDbContext db)
    {
        var assets = new[]
        {
            new Asset { Code = "USD", Name = "US Dollar", Symbol = "$", Type = AssetType.Fiat, DecimalPlaces = 2, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "CAD", Name = "Canadian Dollar", Symbol = "$", Type = AssetType.Fiat, DecimalPlaces = 2, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "GBP", Name = "British Pound", Symbol = "£", Type = AssetType.Fiat, DecimalPlaces = 2, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "EUR", Name = "Euro", Symbol = "€", Type = AssetType.Fiat, DecimalPlaces = 2, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "NGN", Name = "Nigerian Naira", Symbol = "₦", Type = AssetType.Fiat, DecimalPlaces = 2, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "USDT", Name = "Tether USD", Symbol = "USDT", Type = AssetType.Crypto, DecimalPlaces = 18, IsStablecoin = true, IsSupported = true, DepositEnabled = false, WithdrawalEnabled = false, TradingEnabled = true, InstantEnabled = true },
            new Asset { Code = "USDC", Name = "USD Coin", Symbol = "USDC", Type = AssetType.Crypto, DecimalPlaces = 18, IsStablecoin = true, IsSupported = true, DepositEnabled = false, WithdrawalEnabled = false, TradingEnabled = true, InstantEnabled = true }
        };

        foreach (var asset in assets)
        {
            if (!await db.Assets.AnyAsync(x => x.Code == asset.Code))
                db.Assets.Add(asset);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAssetNetworksAsync(AppDbContext db)
    {
        var networks = new[]
        {
            new AssetNetwork { AssetCode = "USDT", NetworkCode = "ETHEREUM", Name = "Ethereum", NativeAssetCode = "ETH", RequiredConfirmations = 12, Status = AssetNetworkStatus.Disabled },
            new AssetNetwork { AssetCode = "USDT", NetworkCode = "TRON", Name = "Tron", NativeAssetCode = "TRX", RequiredConfirmations = 20, Status = AssetNetworkStatus.Disabled },
            new AssetNetwork { AssetCode = "USDT", NetworkCode = "POLYGON", Name = "Polygon", NativeAssetCode = "POL", RequiredConfirmations = 128, Status = AssetNetworkStatus.Disabled },
            new AssetNetwork { AssetCode = "USDC", NetworkCode = "ETHEREUM", Name = "Ethereum", NativeAssetCode = "ETH", RequiredConfirmations = 12, Status = AssetNetworkStatus.Disabled },
            new AssetNetwork { AssetCode = "USDC", NetworkCode = "POLYGON", Name = "Polygon", NativeAssetCode = "POL", RequiredConfirmations = 128, Status = AssetNetworkStatus.Disabled }
        };

        foreach (var network in networks)
        {
            if (!await db.AssetNetworks.AnyAsync(x => x.AssetCode == network.AssetCode && x.NetworkCode == network.NetworkCode))
                db.AssetNetworks.Add(network);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCountryAssetsAsync(AppDbContext db)
    {
        var links = new[]
        {
            new CountryAsset { CountryCode = "US", AssetCode = "USD", CanSend = true, CanReceive = true, CanDeposit = true, CanWithdraw = true, CanTrade = true, CanUseInstant = true, IsDefault = true },
            new CountryAsset { CountryCode = "CA", AssetCode = "CAD", CanSend = true, CanReceive = true, CanDeposit = true, CanWithdraw = true, CanTrade = true, CanUseInstant = true, IsDefault = true },
            new CountryAsset { CountryCode = "GB", AssetCode = "GBP", CanSend = true, CanReceive = true, CanDeposit = true, CanWithdraw = true, CanTrade = true, CanUseInstant = true, IsDefault = true },
            new CountryAsset { CountryCode = "NG", AssetCode = "NGN", CanSend = true, CanReceive = true, CanDeposit = true, CanWithdraw = true, CanTrade = true, CanUseInstant = true, IsDefault = true }
        };

        foreach (var link in links)
        {
            if (!await db.CountryAssets.AnyAsync(x => x.CountryCode == link.CountryCode && x.AssetCode == link.AssetCode))
                db.CountryAssets.Add(link);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedFxAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var rates = new[]
        {
            new ExchangeRate { SourceCurrencyCode = "USD", DestinationCurrencyCode = "NGN", ProviderRate = 1500m, CustomerRate = 1470m, MarkupRate = 30m, ProviderCode = "Blaaiz", EffectiveFrom = now, IsActive = true },
            new ExchangeRate { SourceCurrencyCode = "CAD", DestinationCurrencyCode = "NGN", ProviderRate = 1100m, CustomerRate = 1075m, MarkupRate = 25m, ProviderCode = "Blaaiz", EffectiveFrom = now, IsActive = true }
        };

        foreach (var rate in rates)
        {
            if (!await db.ExchangeRates.AnyAsync(x => x.SourceCurrencyCode == rate.SourceCurrencyCode && x.DestinationCurrencyCode == rate.DestinationCurrencyCode && x.IsActive))
                db.ExchangeRates.Add(rate);
        }

        var fees = new[]
        {
            new TransferFee { SourceCountryCode = "US", DestinationCountryCode = "NG", SourceCurrencyCode = "USD", DestinationCurrencyCode = "NGN", TransferType = TransferType.ConsumerToConsumer, MinAmount = 1m, MaxAmount = 10000m, FixedFee = 2m, PercentageFee = 1.5m, FeeCurrencyCode = "USD", IsActive = true },
            new TransferFee { SourceCountryCode = "CA", DestinationCountryCode = "NG", SourceCurrencyCode = "CAD", DestinationCurrencyCode = "NGN", TransferType = TransferType.ConsumerToConsumer, MinAmount = 1m, MaxAmount = 10000m, FixedFee = 2m, PercentageFee = 1.5m, FeeCurrencyCode = "CAD", IsActive = true }
        };

        foreach (var fee in fees)
        {
            if (!await db.TransferFees.AnyAsync(x => x.SourceCountryCode == fee.SourceCountryCode && x.DestinationCountryCode == fee.DestinationCountryCode && x.SourceCurrencyCode == fee.SourceCurrencyCode && x.DestinationCurrencyCode == fee.DestinationCurrencyCode && x.TransferType == fee.TransferType && x.IsActive))
                db.TransferFees.Add(fee);
        }

        await db.SaveChangesAsync();
    }
}
