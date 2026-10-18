using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.MasterData;
using KorridorX.Models.Lookups;
using KorridorX.Services.Lookups;
using KorridorX.Services.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Tests;

public sealed class CountryAssetBatchTests
{
    [Fact] public void Selection_is_normalized_deduplicated_and_ordered() =>
        Assert.Equal(new[] { "CAD", "NGN" }, CountryAssetBatchRules.NormalizeAssetCodes(["ngn", " cad ", "CAD"]));
    [Fact] public void Null_selection_is_rejected() => Assert.Throws<ArgumentException>(() => CountryAssetBatchRules.NormalizeAssetCodes(null));
    [Fact] public void Empty_selection_is_rejected() => Assert.Throws<ArgumentException>(() => CountryAssetBatchRules.NormalizeAssetCodes([]));
    [Fact] public void Oversized_selection_is_rejected() => Assert.Throws<ArgumentException>(() => CountryAssetBatchRules.NormalizeAssetCodes(Enumerable.Repeat("CAD", 101).ToArray()));
    [Theory][InlineData("")][InlineData(" ")][InlineData("A")][InlineData("CAD;DROP")][InlineData("../NGN")][InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void Invalid_codes_are_rejected(string code) => Assert.Throws<ArgumentException>(() => CountryAssetBatchRules.NormalizeAssetCodes([code]));
    [Fact] public void Hundred_distinct_codes_are_allowed() => Assert.Equal(100, CountryAssetBatchRules.NormalizeAssetCodes(Enumerable.Range(0,100).Select(x => $"AS{x}").ToArray()).Length);
    [Fact] public void Bulk_request_keeps_explicit_flags_but_never_makes_a_default()
    {
        var request = new BatchCountryAssetRequestDto(["CAD"], true, false, true, false, true, false, "reason");
        var flags = CountryAssetBatchRules.Capabilities(request, "reviewed");
        Assert.True(flags.CanSend); Assert.False(flags.CanReceive); Assert.True(flags.CanDeposit);
        Assert.False(flags.CanWithdraw); Assert.True(flags.CanTrade); Assert.False(flags.CanUseInstant);
        Assert.False(flags.IsDefault); Assert.Equal("reviewed", flags.Reason);
    }
    [Theory][InlineData("isDefault")][InlineData("defaultAssetCode")][InlineData("countryCode")][InlineData("availableBalance")]
    public void Unknown_mutation_fields_are_rejected(string field) => Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<BatchCountryAssetRequestDto>("{\"assetCodes\":[\"CAD\"],\"" + field + "\":true}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    [Theory][InlineData("country")][InlineData("asset")][InlineData("send")][InlineData("receive")][InlineData("deposit")][InlineData("withdraw")][InlineData("trade")][InlineData("instant")]
    public void Existing_parent_capability_rules_remain_enforced(string disabled)
    {
        var country = new Country { IsSupported = disabled != "country", IsSendCountry = disabled != "send", IsReceiveCountry = disabled != "receive" };
        var asset = new Asset { IsSupported = disabled != "asset", DepositEnabled = disabled != "deposit", WithdrawalEnabled = disabled != "withdraw", TradingEnabled = disabled != "trade", InstantEnabled = disabled != "instant" };
        var flags = new UpsertCountryAssetRequestDto(true,true,true,true,true,true,false,"reason");
        Assert.Throws<InvalidOperationException>(() => CountryAssetBatchRules.ValidateCountryAssetRequest(country, asset, flags));
    }
    [Fact] public void Disabled_master_records_allow_only_a_disabled_nondefault_mapping() =>
        CountryAssetBatchRules.ValidateCountryAssetRequest(new Country { IsSupported = false }, new Asset { IsSupported = false }, new(false,false,false,false,false,false,false,"reason"));
    [Fact] public void Bulk_route_inherits_strict_master_data_roles_and_rate_limit()
    {
        var type = typeof(AdminMasterDataController);
        Assert.Equal("Admin,SuperAdmin", type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.NotNull(type.GetCustomAttribute<EnableRateLimitingAttribute>());
        var method = type.GetMethod(nameof(AdminMasterDataController.AddCountryAssets))!;
        Assert.Equal("country-assets/{countryCode}/batch", method.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>()); Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal("country-assets/{countryCode}/{assetCode}", type.GetMethod(nameof(AdminMasterDataController.UpsertCountryAsset))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
    }
    [Fact] public void Help_enriches_master_data_retains_existing_master_data_permissions()
    {
        var catalog = new HelpCatalog(); Assert.Equal(85, catalog.Entries.Count(x => !x.IsPage));
        var guides = catalog.Entries.Where(x => x.Portal == "admin" && !x.IsPage && x.PageRoute == "/admin/master-data").ToArray();
        Assert.NotEmpty(guides); Assert.All(guides, x => Assert.Contains("KX-010", x.Keywords));
        Assert.All(guides, x => Assert.Contains(x.Steps, s => s.Instruction.Contains("non-default")));
        Assert.DoesNotContain(catalog.Entries, x => x.Portal == "business" && x.Keywords.Contains("KX-010"));
        var support = Assert.Single(catalog.Entries.Where(x => x.Slug == "kx009-web-operations-support-triage"));
        Assert.Contains(support.Troubleshooting, x => x.Contains("KX-010"));
    }
}
