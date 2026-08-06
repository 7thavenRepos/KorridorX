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
        await SeedCurrenciesAsync(db);
        await SeedCountryCurrenciesAsync(db);
        await SeedFxAsync(db);
    }

    private static async Task SeedCountriesAsync(AppDbContext db)
    {
        var countries = new[]
        {
            new Country { Code = "US", Name = "United States", Iso3Code = "USA", IsSupported = true, IsSendCountry = true, IsReceiveCountry = false },
            new Country { Code = "CA", Name = "Canada", Iso3Code = "CAN", IsSupported = true, IsSendCountry = true, IsReceiveCountry = false },
            new Country { Code = "NG", Name = "Nigeria", Iso3Code = "NGA", IsSupported = true, IsSendCountry = false, IsReceiveCountry = true }
        };

        foreach (var country in countries)
        {
            var exists = await db.Countries.AnyAsync(x => x.Code == country.Code);

            if (!exists)
            {
                db.Countries.Add(country);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCurrenciesAsync(AppDbContext db)
    {
        var currencies = new[]
        {
            new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsFiat = true, IsStablecoin = false, IsSupported = true },
            new Currency { Code = "CAD", Name = "Canadian Dollar", Symbol = "$", DecimalPlaces = 2, IsFiat = true, IsStablecoin = false, IsSupported = true },
            new Currency { Code = "NGN", Name = "Nigerian Naira", Symbol = "₦", DecimalPlaces = 2, IsFiat = true, IsStablecoin = false, IsSupported = true }
        };

        foreach (var currency in currencies)
        {
            var exists = await db.Currencies.AnyAsync(x => x.Code == currency.Code);

            if (!exists)
            {
                db.Currencies.Add(currency);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCountryCurrenciesAsync(AppDbContext db)
    {
        var links = new[]
        {
            new CountryCurrency { CountryCode = "US", CurrencyCode = "USD", CanSend = true, CanReceive = false, IsDefault = true },
            new CountryCurrency { CountryCode = "CA", CurrencyCode = "CAD", CanSend = true, CanReceive = false, IsDefault = true },
            new CountryCurrency { CountryCode = "NG", CurrencyCode = "NGN", CanSend = false, CanReceive = true, IsDefault = true }
        };

        foreach (var link in links)
        {
            var exists = await db.CountryCurrencies.AnyAsync(x =>
                x.CountryCode == link.CountryCode &&
                x.CurrencyCode == link.CurrencyCode);

            if (!exists)
            {
                db.CountryCurrencies.Add(link);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedFxAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var rates = new[]
        {
            new ExchangeRate
            {
                SourceCurrencyCode = "USD",
                DestinationCurrencyCode = "NGN",
                ProviderRate = 1500m,
                CustomerRate = 1470m,
                MarkupRate = 30m,
                ProviderCode = "Blaaiz",
                EffectiveFrom = now,
                IsActive = true
            },
            new ExchangeRate
            {
                SourceCurrencyCode = "CAD",
                DestinationCurrencyCode = "NGN",
                ProviderRate = 1100m,
                CustomerRate = 1075m,
                MarkupRate = 25m,
                ProviderCode = "Blaaiz",
                EffectiveFrom = now,
                IsActive = true
            }
        };

        foreach (var rate in rates)
        {
            var exists = await db.ExchangeRates.AnyAsync(x =>
                x.SourceCurrencyCode == rate.SourceCurrencyCode &&
                x.DestinationCurrencyCode == rate.DestinationCurrencyCode &&
                x.IsActive);

            if (!exists)
            {
                db.ExchangeRates.Add(rate);
            }
        }

        var fees = new[]
        {
            new TransferFee
            {
                SourceCountryCode = "US",
                DestinationCountryCode = "NG",
                SourceCurrencyCode = "USD",
                DestinationCurrencyCode = "NGN",
                TransferType = TransferType.ConsumerToConsumer,
                MinAmount = 1m,
                MaxAmount = 10000m,
                FixedFee = 2m,
                PercentageFee = 1.5m,
                FeeCurrencyCode = "USD",
                IsActive = true
            },
            new TransferFee
            {
                SourceCountryCode = "CA",
                DestinationCountryCode = "NG",
                SourceCurrencyCode = "CAD",
                DestinationCurrencyCode = "NGN",
                TransferType = TransferType.ConsumerToConsumer,
                MinAmount = 1m,
                MaxAmount = 10000m,
                FixedFee = 2m,
                PercentageFee = 1.5m,
                FeeCurrencyCode = "CAD",
                IsActive = true
            }
        };

        foreach (var fee in fees)
        {
            var exists = await db.TransferFees.AnyAsync(x =>
                x.SourceCountryCode == fee.SourceCountryCode &&
                x.DestinationCountryCode == fee.DestinationCountryCode &&
                x.SourceCurrencyCode == fee.SourceCurrencyCode &&
                x.DestinationCurrencyCode == fee.DestinationCurrencyCode &&
                x.TransferType == fee.TransferType &&
                x.IsActive);

            if (!exists)
            {
                db.TransferFees.Add(fee);
            }
        }

        await db.SaveChangesAsync();
    }
}