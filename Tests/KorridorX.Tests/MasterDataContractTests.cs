using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace KorridorX.Tests;

public sealed class MasterDataContractTests
{
    [Fact]
    public void Admin_master_data_controller_is_admin_only()
    {
        var authorize = typeof(AdminMasterDataController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin,SuperAdmin", authorize!.Roles);
    }

    [Fact]
    public void Master_data_controller_exposes_country_and_asset_create_update_routes()
    {
        var methods = typeof(AdminMasterDataController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public);

        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPostAttribute>()?.Template == "countries");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPutAttribute>()?.Template == "countries/{countryCode}");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPostAttribute>()?.Template == "assets");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPutAttribute>()?.Template == "assets/{assetCode}");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpGetAttribute>()?.Template == "country-assets");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPutAttribute>()?.Template ==
            "country-assets/{countryCode}/{assetCode}");
    }

    [Fact]
    public void Digital_asset_admin_exposes_network_creation_route()
    {
        var methods = typeof(AdminDigitalAssetsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public);

        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpPostAttribute>()?.Template ==
            "assets/{assetCode}/networks");
    }

    [Fact]
    public void Fiat_and_crypto_share_the_existing_asset_master()
    {
        Assert.Equal(1, (int)AssetType.Fiat);
        Assert.Equal(2, (int)AssetType.Crypto);

        Assert.Equal(typeof(string), typeof(Asset).GetProperty(nameof(Asset.Code))!.PropertyType);
        Assert.Equal(typeof(AssetType), typeof(Asset).GetProperty(nameof(Asset.Type))!.PropertyType);
        Assert.Equal(typeof(string), typeof(CountryAsset).GetProperty(nameof(CountryAsset.AssetCode))!.PropertyType);
    }

    [Fact]
    public void Lookup_controller_keeps_public_country_and_asset_selectors()
    {
        var methods = typeof(LookupsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public);

        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpGetAttribute>()?.Template == "countries");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpGetAttribute>()?.Template == "assets");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpGetAttribute>()?.Template == "asset-networks");
        Assert.Contains(methods, x =>
            x.GetCustomAttribute<HttpGetAttribute>()?.Template == "corridors");
    }
}
