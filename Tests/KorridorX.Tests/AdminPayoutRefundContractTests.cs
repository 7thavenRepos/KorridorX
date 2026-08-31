using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class AdminPayoutRefundContractTests
{
    [Theory]
    [InlineData(typeof(RefundsController))]
    [InlineData(typeof(AdminProviderRecoveryController))]
    public void Operator_payment_controllers_require_administrative_roles(Type controllerType)
    {
        var authorize = Assert.Single(
            controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal("Admin,SuperAdmin,Operations", authorize.Roles);
    }

    [Fact]
    public void Payout_dispatch_requires_administrative_roles()
    {
        var method = typeof(PayoutsController).GetMethod(
            nameof(PayoutsController.DispatchTransferPayout));
        var authorize = Assert.Single(
            method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal("Admin,SuperAdmin,Operations", authorize.Roles);
    }

    [Fact]
    public void Payout_dispatch_requires_an_audited_reason_body()
    {
        var method = typeof(PayoutsController).GetMethod(
            nameof(PayoutsController.DispatchTransferPayout));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>());
        Assert.Contains(
            method.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(ProviderAdminActionRequestDto) &&
                parameter.GetCustomAttribute<FromBodyAttribute>() is not null);
    }

    [Fact]
    public void Refund_refresh_requires_an_audited_reason_body()
    {
        var method = typeof(RefundsController).GetMethod(
            nameof(RefundsController.RefreshRefund));

        Assert.NotNull(method);
        Assert.Equal(
            "refresh",
            method!.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Contains(
            method.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(ProviderAdminActionRequestDto) &&
                parameter.GetCustomAttribute<FromBodyAttribute>() is not null);
    }
}
