using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Controllers.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class AdminTradingWalletReadContractTests
{
    [Theory]
    [InlineData(typeof(AdminMarketplaceController), "GetPairs", "pairs")]
    [InlineData(typeof(AdminInstantTradingController), "Pairs", "pairs")]
    [InlineData(typeof(AdminInstantTradingController), "HouseAccounts", "house-accounts")]
    [InlineData(typeof(AdminBusinessWalletsController), "GetWallets", null)]
    [InlineData(typeof(AdminBusinessWalletsController), "SearchBusinesses", "businesses")]
    [InlineData(typeof(AdminBusinessWalletsController), "GetLedger", "{walletId:guid}/ledger")]
    public void Read_endpoints_are_explicit_get_contracts(Type controller, string methodName, string? template)
    {
        var method = controller.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var route = Assert.Single(method!.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal(template, route.Template);
    }

    [Theory]
    [InlineData(typeof(AdminMarketplaceController), "Admin,SuperAdmin")]
    [InlineData(typeof(AdminInstantTradingController), "Admin,SuperAdmin,Operations")]
    [InlineData(typeof(AdminBusinessWalletsController), "Admin,SuperAdmin,Operations")]
    public void Controllers_retain_restricted_operator_roles(Type controller, string roles)
    {
        var authorize = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(roles, authorize.Roles);
    }
}
