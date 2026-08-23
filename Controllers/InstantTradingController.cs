using System.Security.Claims;
using KorridorX.Dtos.Instant;
using KorridorX.Infrastructure;
using KorridorX.Services.Instant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize]
[Route("api/instant")]
public sealed class InstantTradingController : ControllerBase
{
    private readonly IInstantTradingService _service;

    public InstantTradingController(IInstantTradingService service)
    {
        _service = service;
    }

    [HttpGet("pairs")]
    [AllowAnonymous]
    public async Task<IActionResult> Pairs(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetPairsAsync(ct), "Instant trading pairs retrieved successfully."));

    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateInstantQuoteRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateQuoteAsync(GetUserId(), request, ct),
            "Instant quote created successfully."));

    [HttpPost("quotes/{quoteId:guid}/execute")]
    public async Task<IActionResult> ExecuteQuote(Guid quoteId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.ExecuteQuoteAsync(GetUserId(), quoteId, ct),
            "Instant trade completed successfully."));

    [HttpGet("trades")]
    public async Task<IActionResult> Trades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetMyTradesAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Instant trades retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
