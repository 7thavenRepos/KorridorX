using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Services.DigitalAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/admin/digital-assets")]
[Authorize(Roles = "Admin,SuperAdmin,Compliance,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminDigitalAssetsController : ControllerBase
{
    private readonly IDigitalAssetEnablementService _service;

    public AdminDigitalAssetsController(
        IDigitalAssetEnablementService service)
    {
        _service = service;
    }

    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetAssetsAsync(ct),
            "Digital assets and networks retrieved successfully."));

    [HttpPut("assets/{assetCode}")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> UpdateAsset(
        string assetCode,
        [FromBody] UpdateDigitalAssetEnablementRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpdateAssetAsync(
                assetCode,
                GetUserId(),
                request,
                ct),
            "Digital-asset enablement updated successfully."));

    [HttpPut("assets/{assetCode}/countries/{countryCode}")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> UpdateCountryAvailability(
        string assetCode,
        string countryCode,
        [FromBody] UpdateDigitalAssetCountryAvailabilityRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpdateCountryAvailabilityAsync(
                assetCode,
                countryCode,
                GetUserId(),
                request,
                ct),
            "Digital-asset country availability updated successfully."));

    [HttpPost("assets/{assetCode}/networks")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> CreateNetwork(
        string assetCode,
        [FromBody] CreateDigitalAssetNetworkRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CreateNetworkAsync(
                assetCode,
                GetUserId(),
                request,
                ct),
            "Digital-asset network created successfully."));

    [HttpPut("networks/{assetNetworkId:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> UpdateNetwork(
        Guid assetNetworkId,
        [FromBody] UpdateDigitalAssetNetworkRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpdateNetworkAsync(
                assetNetworkId,
                GetUserId(),
                request,
                ct),
            "Digital-asset network configuration updated successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException(
                "Invalid authenticated user.");

        return userId;
    }
}