using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessDigitalAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/digital-assets")]
public sealed class BusinessDigitalAssetsController : ControllerBase
{
    private readonly IBusinessDigitalAssetService _service;

    public BusinessDigitalAssetsController(
        IBusinessDigitalAssetService service)
    {
        _service = service;
    }

    [HttpGet("workspace")]
    public async Task<IActionResult> Workspace(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetWorkspaceAsync(GetUserId(), ct),
            "Business digital-asset workspace retrieved successfully."));

    [HttpGet("deposit-addresses")]
    public async Task<IActionResult> DepositAddresses(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetDepositAddressesAsync(GetUserId(), ct),
            "Business digital-asset deposit addresses retrieved successfully."));

    [HttpPost("deposit-addresses")]
    public async Task<IActionResult> CreateDepositAddress(
        [FromBody] CreateDigitalAssetDepositAddressRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateDepositAddressAsync(
                GetUserId(),
                request,
                ct),
            "Business digital-asset deposit address created successfully."));

    [HttpGet("deposit-intents")]
    public async Task<IActionResult> DepositIntents(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetDepositIntentsAsync(GetUserId(), ct),
            "Business digital-asset deposit intents retrieved successfully."));

    [HttpPost("deposit-intents")]
    public async Task<IActionResult> CreateDepositIntent(
        [FromBody] CreateDigitalAssetDepositIntentRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateDepositIntentAsync(
                GetUserId(),
                request,
                ct),
            "Business digital-asset deposit intent created successfully."));

    [HttpGet("withdrawal-destinations")]
    public async Task<IActionResult> WithdrawalDestinations(
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetWithdrawalDestinationsAsync(
                GetUserId(),
                ct),
            "Business digital-asset withdrawal destinations retrieved successfully."));

    [HttpPost("withdrawal-destinations")]
    public async Task<IActionResult> CreateWithdrawalDestination(
        [FromBody] CreateDigitalAssetWithdrawalDestinationRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateWithdrawalDestinationAsync(
                GetUserId(),
                request,
                ct),
            "Business digital-asset withdrawal destination created successfully."));

    [HttpGet("withdrawals")]
    public async Task<IActionResult> Withdrawals(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetWithdrawalsAsync(GetUserId(), ct),
            "Business digital-asset withdrawals retrieved successfully."));

    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal(
        [FromBody] CreateDigitalAssetWithdrawalRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateWithdrawalAsync(
                GetUserId(),
                request,
                ct),
            "Business digital-asset withdrawal submitted successfully."));

    [HttpGet("activity")]
    public async Task<IActionResult> Activity(
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetActivityAsync(
                GetUserId(),
                take,
                ct),
            "Business digital-asset activity retrieved successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Invalid authenticated user.");
    }
}