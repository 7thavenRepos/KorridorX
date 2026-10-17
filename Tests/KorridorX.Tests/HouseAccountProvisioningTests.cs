using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Services.Treasury;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class HouseAccountProvisioningTests
{
    [Fact] public void Normalizes_deduplicates_and_orders_asset_selection() =>
        Assert.Equal(new[] { "CAD", "NGN" }, HouseAccountProvisioningRules.NormalizeAssetCodes([" ngn ", "cad", "CAD"]));
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("../CAD")][InlineData("CAD;DROP")][InlineData("A VERY LONG INVALID ASSET CODE")]
    public void Rejects_invalid_asset_codes(string value) =>
        Assert.Throws<InvalidOperationException>(() => HouseAccountProvisioningRules.NormalizeAssetCodes([value]));
    [Fact] public void Rejects_empty_null_and_oversized_batches()
    {
        Assert.Throws<InvalidOperationException>(() => HouseAccountProvisioningRules.NormalizeAssetCodes([]));
        Assert.Throws<InvalidOperationException>(() => HouseAccountProvisioningRules.NormalizeAssetCodes(null));
        Assert.Throws<InvalidOperationException>(() => HouseAccountProvisioningRules.NormalizeAssetCodes(Enumerable.Repeat("CAD", 51).ToArray()));
    }
    [Fact] public void New_account_is_zero_active_house_and_system_owned()
    {
        var actor = Guid.NewGuid(); var account = HouseAccountProvisioningRules.CreateZeroBalanceAccount("cad", actor);
        Assert.Equal("CAD", account.AssetCode); Assert.Equal("KX-HOUSE-CAD", account.AccountCode);
        Assert.Equal(FinancialAccountOwnerType.Treasury, account.OwnerType);
        Assert.Equal(FinancialAccountType.House, account.AccountType); Assert.Equal(FinancialAccountStatus.Active, account.Status);
        Assert.Equal(HouseAccountProvisioningRules.TreasuryOwnerId, account.OwnerId); Assert.NotEqual(actor, account.OwnerId);
        Assert.Equal(actor, account.CreatedByUserId);
        Assert.Equal(0m, account.SettledBalance); Assert.Equal(0m, account.AvailableBalance); Assert.Equal(0m, account.HeldBalance);
        Assert.False(account.IsDeleted);
    }
    [Fact] public void Empty_actor_is_rejected() => Assert.Throws<UnauthorizedAccessException>(() => HouseAccountProvisioningRules.CreateZeroBalanceAccount("CAD", Guid.Empty));
    [Theory]
    [InlineData("openingBalance")][InlineData("ownerId")][InlineData("status")][InlineData("availableBalance")]
    public void Request_rejects_injected_fields(string field)
    {
        var json = "{\"assetCodes\":[\"CAD\"],\"" + field + "\":100}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProvisionHouseAccountsRequestDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
    [Fact] public void Provisioning_has_only_reviewed_admin_routes_and_roles()
    {
        var type = typeof(AdminHouseAccountsController);
        Assert.Equal("Admin,SuperAdmin,Operations", type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/admin/treasury/house-accounts", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        var method = type.GetMethod(nameof(AdminHouseAccountsController.Provision))!;
        Assert.Equal("provision", method.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.NotNull(type.GetMethod(nameof(AdminHouseAccountsController.List))!.GetCustomAttribute<HttpGetAttribute>());
    }
}
