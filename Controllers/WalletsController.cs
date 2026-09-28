using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Wallets;
using KorridorX.Infrastructure;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Consumer")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly IConsumerWalletService _wallets;

    public WalletsController(IConsumerWalletService wallets)
    {
        _wallets = wallets;
    }

    [HttpGet]
    public async Task<IActionResult> GetWallets(CancellationToken ct)
    {
        var result = await _wallets.GetWalletsAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallets retrieved successfully."));
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableWalletAssets(CancellationToken ct)
    {
        var result = await _wallets.GetAvailableAssetsAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(
            result,
            "Available wallet assets retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWallet(
        [FromBody] CreateConsumerWalletRequestDto request,
        CancellationToken ct)
    {
        var result = await _wallets.CreateWalletAsync(GetUserId(), request, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet is ready."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
