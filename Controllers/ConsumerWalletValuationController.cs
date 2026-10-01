using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Infrastructure;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Consumer")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallets/valuation")]
public sealed class ConsumerWalletValuationController : ControllerBase
{
    private readonly IConsumerWalletValuationService _valuation;

    public ConsumerWalletValuationController(IConsumerWalletValuationService valuation) => _valuation = valuation;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetValuation([FromQuery] string baseAssetCode, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id))
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        var result = await _valuation.GetValuationAsync(id, baseAssetCode, ct);
        return Ok(ApiResponses.Ok(result, "Estimated wallet valuation retrieved successfully."));
    }
}
