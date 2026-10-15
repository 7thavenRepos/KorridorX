using System.Security.Claims;
using KorridorX.Dtos.Instant;
using KorridorX.Infrastructure;
using KorridorX.Services.Instant;
using KorridorX.Services.Security;
using KorridorX.Services.Compliance;
using KorridorX.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize]
[Route("api/instant")]
public sealed class InstantTradingController : ControllerBase
{
    private readonly IInstantTradingService _service;
    private readonly ITransactionPinService _pin;
    private readonly IOutboundFundsRestrictionService _restrictions;

    public InstantTradingController(
        IInstantTradingService service,
        ITransactionPinService pin,
        IOutboundFundsRestrictionService restrictions)
    {
        _service = service;
        _pin = pin;
        _restrictions = restrictions;
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
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ExecuteQuote(
        Guid quoteId,
        [FromBody] ExecuteInstantQuoteRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        await _restrictions.EnsureUserOutboundAllowedAsync(userId, "consumer_instant_trade", ct);
        if (request is null || string.IsNullOrWhiteSpace(request.TransactionPin))
            throw new InvalidOperationException("Transaction PIN is required to exchange wallet balances.");
        await _pin.VerifyForTransactionAsync(userId, request.TransactionPin, ct);
        return Ok(ApiResponses.Ok(
            await _service.ExecuteQuoteAsync(userId, quoteId, ct),
            "Wallet exchange completed successfully."));
    }

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
