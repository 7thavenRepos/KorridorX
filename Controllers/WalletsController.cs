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
    private readonly IConsumerWalletFundingService _funding;

    public WalletsController(
        IConsumerWalletService wallets,
        IConsumerWalletFundingService funding)
    {
        _wallets = wallets;
        _funding = funding;
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

    [HttpGet("{walletId:guid}/funding-methods")]
    public async Task<IActionResult> GetFundingMethods(
        [FromRoute] Guid walletId,
        CancellationToken ct)
    {
        var result = await _funding.GetFundingMethodsAsync(GetUserId(), walletId, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding methods retrieved successfully."));
    }

    [HttpPost("{walletId:guid}/collections")]
    public async Task<IActionResult> CreateFundingCollection(
        [FromRoute] Guid walletId,
        [FromBody] CreateConsumerWalletFundingCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _funding.CreateCollectionAsync(
            GetUserId(),
            walletId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection created successfully."));
    }

    [HttpPost("{walletId:guid}/collections/{collectionId:guid}/initiate")]
    public async Task<IActionResult> InitiateFundingCollection(
        [FromRoute] Guid walletId,
        [FromRoute] Guid collectionId,
        [FromBody] KorridorX.Dtos.Payments.InitiateCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _funding.InitiateCollectionAsync(
            GetUserId(),
            walletId,
            collectionId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection initiated successfully."));
    }

    [HttpGet("{walletId:guid}/collections/{collectionId:guid}")]
    public async Task<IActionResult> GetFundingCollection(
        [FromRoute] Guid walletId,
        [FromRoute] Guid collectionId,
        CancellationToken ct)
    {
        var result = await _funding.GetCollectionAsync(
            GetUserId(),
            walletId,
            collectionId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
