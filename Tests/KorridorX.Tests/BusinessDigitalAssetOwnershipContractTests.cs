using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Models.Customers;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Tests;

public sealed class BusinessDigitalAssetOwnershipContractTests
{
    [Theory]
    [InlineData(typeof(DigitalAssetDepositAddress))]
    [InlineData(typeof(DigitalAssetDepositIntent))]
    [InlineData(typeof(DigitalAssetWithdrawalDestination))]
    [InlineData(typeof(DigitalAssetWithdrawal))]
    [InlineData(typeof(DigitalAssetTravelRuleRecord))]
    [InlineData(typeof(DigitalAssetAddressRiskAssessment))]
    public void Business_customer_scope_is_nullable_for_direct_business_ownership(
        Type entityType)
    {
        var property = entityType.GetProperty(
            "BusinessCustomerId",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(Guid?), property!.PropertyType);
    }

    [Fact]
    public void Digital_asset_permissions_extend_business_permission_mask()
    {
        Assert.Equal(1L << 17, (long)BusinessPermission.ViewDigitalAssets);
        Assert.Equal(1L << 18, (long)BusinessPermission.ManageDigitalAssets);
        Assert.True(BusinessPermission.All.HasFlag(BusinessPermission.ViewDigitalAssets));
        Assert.True(BusinessPermission.All.HasFlag(BusinessPermission.ManageDigitalAssets));
    }

    [Fact]
    public void Finance_can_manage_digital_assets_but_compliance_is_read_only()
    {
        var finance = BusinessAccessService.DefaultPermissions(
            BusinessUserRole.Finance);
        var compliance = BusinessAccessService.DefaultPermissions(
            BusinessUserRole.Compliance);

        Assert.True(finance.HasFlag(BusinessPermission.ViewDigitalAssets));
        Assert.True(finance.HasFlag(BusinessPermission.ManageDigitalAssets));

        Assert.True(compliance.HasFlag(BusinessPermission.ViewDigitalAssets));
        Assert.False(compliance.HasFlag(BusinessPermission.ManageDigitalAssets));
    }

    [Fact]
    public void Business_digital_asset_controller_is_authenticated_sensitive_and_business_scoped()
    {
        var type = typeof(BusinessDigitalAssetsController);

        Assert.Single(
            type.GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Single(
            type.GetCustomAttributes(
                    typeof(EnableRateLimitingAttribute),
                    inherit: true)
                .Cast<EnableRateLimitingAttribute>());

        var route = Assert.Single(
            type.GetCustomAttributes(
                    typeof(RouteAttribute),
                    inherit: true)
                .Cast<RouteAttribute>());

        Assert.Equal(
            "api/business/digital-assets",
            route.Template);
    }
}