using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers.Admin;

[Authorize(Roles = "Admin,SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/marketplace")]
public class AdminMarketplaceController : ControllerBase
{
    private readonly IMarketplaceOperationsService _marketplace;

    public AdminMarketplaceController(IMarketplaceOperationsService marketplace)
    {
        _marketplace = marketplace;
    }

    [HttpPost("pairs")]
    public async Task<IActionResult> CreatePair(
        [FromBody] CreateMarketplacePairRequestDto request,
        CancellationToken ct)
    {
        var result = await _marketplace.CreatePairAsync(request, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Marketplace pair created successfully."));
    }

    [HttpPut("pairs/{pairId:guid}")]
    public async Task<IActionResult> UpdatePair(
        Guid pairId,
        [FromBody] UpdateMarketplacePairRequestDto request,
        CancellationToken ct)
    {
        var result = await _marketplace.UpdatePairAsync(pairId, request, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Marketplace pair updated successfully."));
    }

    [HttpPut("pairs/{pairId:guid}/status")]
    public async Task<IActionResult> SetPairStatus(
        Guid pairId,
        [FromBody] SetMarketplacePairStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _marketplace.SetPairStatusAsync(pairId, request.Status, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Marketplace pair status updated successfully."));
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? pairId,
        [FromQuery] TradeOrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _marketplace.GetOrdersAsync(pairId, status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace orders retrieved successfully."));
    }

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches(
        [FromQuery] Guid? pairId,
        [FromQuery] TradeMatchStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _marketplace.GetMatchesAsync(pairId, status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace matches retrieved successfully."));
    }

    [HttpGet("trades")]
    public async Task<IActionResult> GetTrades(
        [FromQuery] Guid? pairId,
        [FromQuery] TradeStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _marketplace.GetTradesAsync(pairId, status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace trades retrieved successfully."));
    }

    [HttpPost("maintenance/expire-orders")]
    public async Task<IActionResult> ExpireOrders(
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var result = await _marketplace.ExpireOrdersAsync(take, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Marketplace order expiry maintenance completed."));
    }

    [HttpPost("maintenance/recover-settlements")]
    public async Task<IActionResult> RecoverSettlements(
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _marketplace.RecoverSettlementsAsync(take, ct);
        return Ok(ApiResponses.Ok(result, "Marketplace settlement recovery completed."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
