using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Providers.Screening;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

public sealed class ConfiguredWatchlistScreeningProviderTests
{
    [Fact]
    public async Task ScreenAsync_ReturnsConfirmedMatchForBlockingExactMatch()
    {
        var provider = CreateProvider(new ConfiguredWatchlistEntry
        {
            Id = "entry-1",
            Name = "Ada Nkem Okafor",
            WatchlistType = WatchlistType.Sanctions,
            ListName = "Unit Test List",
            CountryCode = "NG",
            IsBlocking = true
        });

        var result = await provider.ScreenAsync(new ScreeningProviderRequest(
            ScreeningSubjectType.Customer,
            Guid.NewGuid(),
            "Ada Nkem Okafor",
            [],
            "NG",
            null,
            null,
            ScreeningReason.Manual));

        Assert.Equal(ScreeningStatus.ConfirmedMatch, result.Status);
        Assert.True(result.IsBlocking);
        Assert.Equal(100m, result.HighestMatchScore);
        Assert.Single(result.Matches);
    }

    [Fact]
    public async Task ScreenAsync_ReturnsClearWhenNoEntryMatches()
    {
        var provider = CreateProvider(new ConfiguredWatchlistEntry
        {
            Id = "entry-1",
            Name = "Unrelated Person",
            WatchlistType = WatchlistType.Pep,
            ListName = "Unit Test List",
            IsBlocking = false
        });

        var result = await provider.ScreenAsync(new ScreeningProviderRequest(
            ScreeningSubjectType.Recipient,
            Guid.NewGuid(),
            "Ada Nkem Okafor",
            [],
            "NG",
            null,
            null,
            ScreeningReason.Onboarding));

        Assert.Equal(ScreeningStatus.Clear, result.Status);
        Assert.False(result.IsBlocking);
        Assert.Empty(result.Matches);
    }

    private static ConfiguredWatchlistScreeningProvider CreateProvider(
        params ConfiguredWatchlistEntry[] entries)
    {
        return new ConfiguredWatchlistScreeningProvider(Options.Create(
            new ComplianceScreeningOptions
            {
                IsEnabled = true,
                PotentialMatchScore = 80m,
                BlockingMatchScore = 95m,
                Entries = entries.ToList()
            }));
    }
}
