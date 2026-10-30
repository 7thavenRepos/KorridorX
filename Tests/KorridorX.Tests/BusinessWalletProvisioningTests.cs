using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessFunding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class BusinessWalletProvisioningTests
{
    [Fact]
    public void New_business_defaults_to_direct_transfer_without_overwriting_existing_ones()
    {
        var profile = new BusinessProfile();
        Assert.False(profile.RequiresTransferApproval);
        Assert.Equal(1, profile.RequiredTransferApprovals);
        Assert.False(profile.AllowTransferCreatorApproval);
    }

    [Theory]
    [InlineData(" ngn ", "NGN")]
    [InlineData("cad", "CAD")]
    public void Fiat_asset_code_normalization_is_deterministic(string raw, string expected) =>
        Assert.Equal(expected, BusinessWalletProvisioningRules.NormalizeAssetCode(raw));

    [Theory]
    [InlineData("")][InlineData("usd;drop")][InlineData("USDT")][InlineData("../NGN")]
    public void Invalid_fiat_code_is_rejected(string raw) =>
        Assert.Throws<InvalidOperationException>(() =>
            BusinessWalletProvisioningRules.NormalizeAssetCode(raw));

    [Fact]
    public void New_wallet_is_active_business_owned_and_unfunded()
    {
        var business = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var account = BusinessWalletProvisioningRules.CreateZeroBalanceAccount(business, "ngn", actor);
        Assert.Equal(FinancialAccountOwnerType.Business, account.OwnerType);
        Assert.Equal(FinancialAccountType.Customer, account.AccountType);
        Assert.Equal(FinancialAccountStatus.Active, account.Status);
        Assert.Equal(business, account.OwnerId);
        Assert.Equal(actor, account.CreatedByUserId);
        Assert.Equal("BUS-" + business.ToString("N") + "-NGN", account.AccountCode);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.HeldBalance);
    }

    [Theory]
    [InlineData("ownerId")][InlineData("amount")][InlineData("availableBalance")]
    public void Caller_cannot_inject_wallet_owner_or_balance(string field)
    {
        var json = "{\"assetCode\":\"NGN\",\"" + field + "\":100}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateBusinessWalletRequestDto>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Business_wallet_endpoints_are_authenticated_and_route_scoped()
    {
        var type = typeof(BusinessFundingController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/business/funding", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal("wallets", type.GetMethod("CreateWallet")?
            .GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal("wallets/available", type.GetMethod("GetAvailableWalletAssets")?
            .GetCustomAttribute<HttpGetAttribute>()?.Template);
    }
}
