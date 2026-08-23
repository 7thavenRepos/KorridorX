using System.Security.Claims;
using KorridorX.Dtos.Instant;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Instant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/instant")]
public sealed class AdminInstantTradingController : ControllerBase
{
    private readonly IInstantTradingService _service;

    public AdminInstantTradingController(IInstantTradingService service)
    {
        _service = service;
    }

    [HttpPost("pairs")]
    public async Task<IActionResult> CreatePair(
        [FromBody] CreateInstantPairRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreatePairAsync(GetUserId(), request, ct),
            "Instant trading pair created successfully."));

    [HttpPut("pairs/{pairId:guid}")]
    public async Task<IActionResult> UpdatePair(
        Guid pairId,
        [FromBody] UpdateInstantPairRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpdatePairAsync(GetUserId(), pairId, request, ct),
            "Instant trading pair updated successfully."));

    [HttpPut("pairs/{pairId:guid}/status")]
    public async Task<IActionResult> SetPairStatus(
        Guid pairId,
        [FromBody] SetInstantPairStatusRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.SetPairStatusAsync(GetUserId(), pairId, request.Status, ct),
            "Instant trading pair status updated successfully."));

    [HttpGet("quotes")]
    public async Task<IActionResult> Quotes(
        [FromQuery] InstantQuoteStatus? status = null,
        [FromQuery] Guid? pairId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetAdminQuotesAsync(status, pairId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Instant quotes retrieved successfully."));
    }

    [HttpGet("trades")]
    public async Task<IActionResult> Trades(
        [FromQuery] InstantTradeStatus? status = null,
        [FromQuery] Guid? pairId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetAdminTradesAsync(status, pairId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Instant trades retrieved successfully."));
    }

    [HttpGet("liquidity")]
    public async Task<IActionResult> Liquidity(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetLiquidityAsync(ct),
            "Instant house liquidity retrieved successfully."));

    [HttpGet("settlement-exceptions")]
    public async Task<IActionResult> SettlementExceptions(
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetSettlementExceptionsAsync(take, ct),
            "Instant settlement exceptions retrieved successfully."));

    [HttpPost("maintenance/expire-quotes")]
    public async Task<IActionResult> ExpireQuotes(
        [FromQuery] int take = 500,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.ExpireQuotesAsync(take, ct),
            "Instant quote expiry maintenance completed."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
