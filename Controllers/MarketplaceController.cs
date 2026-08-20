using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Services.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/marketplace")]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceOrderService _orders;
    private readonly IMarketplaceOperationsService _operations;

    public MarketplaceController(
        IMarketplaceOrderService orders,
        IMarketplaceOperationsService operations)
    {
        _orders = orders;
        _operations = operations;
    }

    [HttpGet("pairs")]
    public async Task<IActionResult> GetPairs(CancellationToken ct)
    {
        var result = await _orders.GetActivePairsAsync(ct);
        return Ok(ApiResponses.Ok(result, "Marketplace pairs retrieved successfully."));
    }

    [HttpGet("pairs/{marketplacePairId:guid}/orderbook")]
    public async Task<IActionResult> GetOrderBook(
        Guid marketplacePairId,
        [FromQuery] int depth = 20,
        CancellationToken ct = default)
    {
        var result = await _orders.GetOrderBookAsync(marketplacePairId, depth, ct);
        return Ok(ApiResponses.Ok(result, "Marketplace order book retrieved successfully."));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateTradeOrderRequestDto request,
        CancellationToken ct)
    {
        var result = await _orders.CreateOrderAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Marketplace order created successfully."));
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _orders.GetMyOrdersAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace orders retrieved successfully."));
    }

    [HttpGet("trades")]
    public async Task<IActionResult> GetMyTrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _operations.GetMyTradesAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace trades retrieved successfully."));
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _orders.GetOrderAsync(GetUserId(), orderId, ct);
        return Ok(ApiResponses.Ok(result, "Marketplace order retrieved successfully."));
    }

    [HttpPost("orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _orders.CancelOrderAsync(GetUserId(), orderId, ct);
        return Ok(ApiResponses.Ok(result, "Marketplace order cancelled successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
