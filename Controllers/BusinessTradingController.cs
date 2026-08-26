using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.BusinessTrading;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTrading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/trading")]
public sealed class BusinessTradingController : ControllerBase
{
    private readonly IBusinessTradingService _service;

    public BusinessTradingController(IBusinessTradingService service)
    {
        _service = service;
    }

    [HttpGet("workspace")]
    public async Task<IActionResult> Workspace(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetWorkspaceAsync(GetUserId(), ct),
            "Business trading workspace retrieved successfully."));

    [HttpGet("balances")]
    public async Task<IActionResult> Balances(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetBalancesAsync(GetUserId(), ct),
            "Business trading balances retrieved successfully."));

    [HttpGet("marketplace/pairs")]
    public async Task<IActionResult> MarketplacePairs(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetMarketplacePairsAsync(GetUserId(), ct),
            "Business marketplace pairs retrieved successfully."));

    [HttpGet("marketplace/pairs/{pairId:guid}/orderbook")]
    public async Task<IActionResult> MarketplaceOrderBook(
        Guid pairId,
        [FromQuery] int depth = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetOrderBookAsync(GetUserId(), pairId, depth, ct),
            "Business marketplace order book retrieved successfully."));

    [HttpPost("marketplace/orders")]
    public async Task<IActionResult> CreateMarketplaceOrder(
        [FromBody] CreateTradeOrderRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateMarketplaceOrderAsync(GetUserId(), request, ct),
            "Business marketplace order created successfully."));

    [HttpGet("marketplace/orders")]
    public async Task<IActionResult> MarketplaceOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetMarketplaceOrdersAsync(
            GetUserId(),
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Business marketplace orders retrieved successfully."));
    }

    [HttpGet("marketplace/orders/{orderId:guid}")]
    public async Task<IActionResult> MarketplaceOrder(
        Guid orderId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetMarketplaceOrderAsync(GetUserId(), orderId, ct),
            "Business marketplace order retrieved successfully."));

    [HttpPost("marketplace/orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelMarketplaceOrder(
        Guid orderId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CancelMarketplaceOrderAsync(GetUserId(), orderId, ct),
            "Business marketplace order cancelled successfully."));

    [HttpGet("marketplace/trades")]
    public async Task<IActionResult> MarketplaceTrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetMarketplaceTradesAsync(
            GetUserId(),
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Business marketplace trades retrieved successfully."));
    }

    [HttpGet("instant/pairs")]
    public async Task<IActionResult> InstantPairs(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetInstantPairsAsync(GetUserId(), ct),
            "Business instant pairs retrieved successfully."));

    [HttpPost("instant/quotes")]
    public async Task<IActionResult> CreateInstantQuote(
        [FromBody] CreateInstantQuoteRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateInstantQuoteAsync(GetUserId(), request, ct),
            "Business instant quote created successfully."));

    [HttpPost("instant/quotes/{quoteId:guid}/execute")]
    public async Task<IActionResult> ExecuteInstantQuote(
        Guid quoteId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.ExecuteInstantQuoteAsync(GetUserId(), quoteId, ct),
            "Business instant trade completed successfully."));

    [HttpGet("instant/trades")]
    public async Task<IActionResult> InstantTrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetInstantTradesAsync(
            GetUserId(),
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Business instant trades retrieved successfully."));
    }

    [HttpGet("counterparties")]
    public async Task<IActionResult> Counterparties(
        [FromQuery] string? search = null,
        [FromQuery] Guid? marketplacePairId = null,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.SearchCounterpartiesAsync(
                GetUserId(),
                search,
                marketplacePairId,
                take,
                ct),
            "Business trading counterparties retrieved successfully."));

    [HttpPost("rfqs")]
    public async Task<IActionResult> CreateRfq(
        [FromBody] CreateBusinessProfileTradingRfqRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateRfqAsync(GetUserId(), request, ct),
            "Business trading RFQ created successfully."));

    [HttpGet("rfqs")]
    public async Task<IActionResult> Rfqs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetRfqsAsync(
            GetUserId(),
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Business trading RFQs retrieved successfully."));
    }

    [HttpGet("rfqs/{rfqId:guid}")]
    public async Task<IActionResult> Rfq(
        Guid rfqId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetRfqAsync(GetUserId(), rfqId, ct),
            "Business trading RFQ retrieved successfully."));

    [HttpPost("rfqs/{rfqId:guid}/quotes")]
    public async Task<IActionResult> QuoteRfq(
        Guid rfqId,
        [FromBody] CreateBusinessProfileTradingRfqQuoteRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.QuoteRfqAsync(GetUserId(), rfqId, request, ct),
            "Business trading RFQ quote submitted successfully."));

    [HttpPost("rfqs/{rfqId:guid}/quotes/{quoteId:guid}/accept")]
    public async Task<IActionResult> AcceptRfqQuote(
        Guid rfqId,
        Guid quoteId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.AcceptRfqQuoteAsync(
                GetUserId(),
                rfqId,
                quoteId,
                ct),
            "Business trading RFQ quote accepted successfully."));

    [HttpPost("rfqs/{rfqId:guid}/cancel")]
    public async Task<IActionResult> CancelRfq(
        Guid rfqId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CancelRfqAsync(GetUserId(), rfqId, ct),
            "Business trading RFQ cancelled successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Invalid authenticated user.");
    }
}