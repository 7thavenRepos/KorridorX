using System.Security.Claims;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Services.Lookups;
using KorridorX.Services.BusinessFunding;
using KorridorX.Tests.SearchHelp;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KorridorX.Tests;

public sealed class AssetChannelAvailabilityTests
{
    [Fact]
    public void Existing_assets_default_to_both_channels()
    {
        var asset = new Asset { Code = "CAD" };
        Assert.True(asset.IsSupported);
        Assert.True(asset.IndividualEnabled);
        Assert.True(asset.BusinessEnabled);
    }

    [Fact]
    public void Seven_individual_assets_are_listed_in_both_selectors_for_each_supported_country()
    {
        var countries = new[] { new LookupCountry("CA", "Canada"), new LookupCountry("NG", "Nigeria") };
        var assets = new[] { "NGN", "USD", "CAD", "GBP", "EUR", "USDT", "USDC" }
            .Select(x => new LookupAsset(x, x, x.StartsWith("USDT") || x.StartsWith("USDC")
                ? AssetType.Crypto : AssetType.Fiat)).ToArray();
        var mapped = new[] { new LookupMapping("CA", "CAD", true, false, true, false, false, false, true) };
        var options = AssetSelectionCatalog.Build(countries, assets, mapped);

        Assert.Equal(14, options.Count);
        foreach (var country in countries)
        {
            Assert.Equal(7, options.Count(x => x.CountryCode == country.Code && x.CanSend));
            Assert.Equal(7, options.Count(x => x.CountryCode == country.Code && x.CanReceive));
        }
        Assert.True(options.Single(x => x.CountryCode == "CA" && x.AssetCode == "CAD").ActualCanSend);
        Assert.False(options.Single(x => x.CountryCode == "NG" && x.AssetCode == "CAD").ActualCanSend);
        Assert.False(options.Single(x => x.CountryCode == "CA" && x.AssetCode == "USDT").CanDeposit);
    }

    [Fact]
    public void Business_lookup_is_explicit_and_dual_role_consumers_default_to_individual()
    {
        var individual = UserWithRole("Consumer");
        var business = UserWithRole("Business");
        var dualRole = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, "Consumer"), new Claim(ClaimTypes.Role, "Business") }, "Test"));
        Assert.Equal(AssetLookupChannel.Individual, AssetChannelAccessPolicy.ResolveChannel(null, individual));
        Assert.Equal(AssetLookupChannel.Individual, AssetChannelAccessPolicy.ResolveChannel(null, dualRole));
        Assert.Equal(AssetLookupChannel.Business, AssetChannelAccessPolicy.ResolveChannel("business", business));
        Assert.Equal(AssetLookupChannel.Business, AssetChannelAccessPolicy.ResolveChannel("business", dualRole));
        Assert.Throws<UnauthorizedAccessException>(() =>
            AssetChannelAccessPolicy.ResolveChannel("business", individual));
        Assert.Throws<UnauthorizedAccessException>(() =>
            AssetChannelAccessPolicy.ResolveChannel("admin", business));
        Assert.Equal(AssetLookupChannel.AdminAll, AssetChannelAccessPolicy.ResolveChannel("admin", UserWithRole("SuperAdmin")));
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"));

    [Fact]
    public void Channel_asset_input_can_be_pre_filtered_without_changing_lookup_contract()
    {
        var all = new[]
        {
            new Asset { Code = "USD", IsSupported = true },
            new Asset { Code = "USDT", IsSupported = true, IndividualEnabled = false },
            new Asset { Code = "USDC", IsSupported = true, BusinessEnabled = false },
            new Asset { Code = "EUR", IsSupported = false },
        };
        var individual = all.Where(x => x.IsSupported && x.IndividualEnabled).Select(x => x.Code).ToArray();
        var business = all.Where(x => x.IsSupported && x.BusinessEnabled).Select(x => x.Code).ToArray();
        Assert.Equal(new[] { "USD", "USDC" }, individual);
        Assert.Equal(new[] { "USD", "USDT" }, business);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("corp", "usd", FinancialAccountStatus.Active)]
    [InlineData("  corp  ", "CAD", FinancialAccountStatus.Frozen)]
    public void Admin_wallet_listing_translates_to_PostgreSQL_without_connecting(
        string? search, string? asset, FinancialAccountStatus? status)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = AdminBusinessWalletQuery.Build(db, null, search, asset, status)
            .Skip(2).Take(7).ToQueryString();
        Assert.Contains("SELECT", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }
}
