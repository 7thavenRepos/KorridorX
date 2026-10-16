using KorridorX.Controllers;
using KorridorX.Dtos.Marketplace;
using KorridorX.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace KorridorX.Tests;

public sealed class MarketplaceConsumerOrderContractTests
{
    [Fact]
    public void Consumer_order_create_keeps_legacy_request_compatibility_but_has_PIN_field()
    {
        var request = new CreateTradeOrderRequestDto(
            Guid.NewGuid(), TradeOrderSide.Sell, TradeOrderType.Limit, 10m, 1250m);
        Assert.Null(request.TransactionPin);
        Assert.Equal(TradeOrderTimeInForce.GoodTillCancelled, request.TimeInForce);
        Assert.Equal("999999", (request with { TransactionPin = "999999" }).TransactionPin);
    }

    [Fact]
    public void Marketplace_consumer_endpoints_remain_authorized_and_rate_limited()
    {
        var type = typeof(MarketplaceController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        var route = type.GetCustomAttribute<RouteAttribute>();
        Assert.Equal("api/marketplace", route?.Template);
        var action = type.GetMethod(nameof(MarketplaceController.CreateOrder));
        Assert.NotNull(action);
        Assert.Contains(action!.GetCustomAttributes<HttpPostAttribute>(),
            attribute => attribute.Template == "orders");
    }
}
