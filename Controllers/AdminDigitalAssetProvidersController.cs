using System.Security.Claims;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Services.DigitalAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/admin/digital-asset-providers")]
[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
public sealed class AdminDigitalAssetProvidersController : ControllerBase
{
    private readonly IDigitalAssetProviderOperationsService _service;

    public AdminDigitalAssetProvidersController(
        IDigitalAssetProviderOperationsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfigurations(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetConfigurationsAsync(ct),
            "Digital-asset provider configurations retrieved successfully."));

    [HttpPut]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertDigitalAssetProviderConfigurationRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpsertConfigurationAsync(GetUserId(), request, ct),
            "Digital-asset provider configuration saved successfully."));

    [HttpPost("{providerCode}/health")]
    public async Task<IActionResult> Health(
        string providerCode,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CheckHealthAsync(providerCode, ct),
            "Digital-asset provider health check completed."));

    [HttpPost("{providerCode}/balances/sync")]
    public async Task<IActionResult> SyncBalances(
        string providerCode,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.SyncBalancesAsync(providerCode, ct),
            "Digital-asset provider balances synchronized successfully."));

    [HttpGet("fee-exceptions")]
    public async Task<IActionResult> FeeExceptions(
        [FromQuery] decimal minimumAbsoluteVariance = 0.00000001m,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetFeeExceptionsAsync(
                minimumAbsoluteVariance,
                take,
                ct),
            "Digital-asset network-fee exceptions retrieved successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid authenticated user.");

        return userId;
    }

}
