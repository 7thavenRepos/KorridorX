using KorridorX.Controllers;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class BusinessTradingPortalContractTests
{
    [Fact]
    public void Trading_permissions_are_in_All_and_do_not_overlap_existing_bits()
    {
        Assert.True(BusinessPermission.All.HasFlag(BusinessPermission.ViewTrading));
        Assert.True(BusinessPermission.All.HasFlag(BusinessPermission.Trade));
        Assert.NotEqual(BusinessPermission.ViewTrading, BusinessPermission.Trade);
        Assert.Equal(1L << 15, (long)BusinessPermission.ViewTrading);
        Assert.Equal(1L << 16, (long)BusinessPermission.Trade);
    }

    [Fact]
    public void Finance_can_trade_but_compliance_is_trading_read_only()
    {
        var finance = BusinessAccessService.DefaultPermissions(BusinessUserRole.Finance);
        var compliance = BusinessAccessService.DefaultPermissions(BusinessUserRole.Compliance);

        Assert.True(finance.HasFlag(BusinessPermission.ViewTrading));
        Assert.True(finance.HasFlag(BusinessPermission.Trade));

        Assert.True(compliance.HasFlag(BusinessPermission.ViewTrading));
        Assert.False(compliance.HasFlag(BusinessPermission.Trade));
    }

    [Fact]
    public void Business_trading_controller_is_authenticated_and_business_scoped()
    {
        var type = typeof(BusinessTradingController);

        Assert.NotNull(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());

        var route = Assert.Single(
            type.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>());

        Assert.Equal("api/business/trading", route.Template);
    }
}