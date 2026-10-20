using System.Security.Claims;
using KorridorX.Dtos.Treasury;
using KorridorX.Dtos.Fx;
using KorridorX.Infrastructure;
using KorridorX.Services.Fx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/fx")]
public sealed class AdminFxOperationsController : ControllerBase
{
    private readonly IFxOperationsService _service;
    private readonly IFxProviderRateService _providers;
    public AdminFxOperationsController(IFxOperationsService service, IFxProviderRateService providers)
    { _service = service; _providers = providers; }

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.GetOptionsAsync(ct), "FX pair and provider options retrieved."));

    [HttpGet("configurations")]
    public async Task<IActionResult> Configurations(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.GetConfigurationsAsync(ct), "FX provider configurations retrieved."));

    [HttpPost("preview")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Preview([FromBody] SaveFxProviderConfigurationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.PreviewAsync(request, ct), "Live provider rate checked. This preview is not an executable quote."));

    [HttpPost("configurations")]
    public async Task<IActionResult> CreateConfiguration([FromBody] SaveFxProviderConfigurationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.SaveAsync(GetUserId(), null, request, ct), "FX provider draft saved."));

    [HttpPut("configurations/{id:guid}")]
    public async Task<IActionResult> UpdateConfiguration(Guid id, [FromBody] SaveFxProviderConfigurationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.SaveAsync(GetUserId(), id, request, ct), "FX provider draft updated."));

    [HttpPost("configurations/{id:guid}/activate")]
    public async Task<IActionResult> ActivateConfiguration(Guid id, [FromBody] FxConfigurationRevisionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.ActivateAsync(GetUserId(), id, request.Revision, ct), "FX provider configuration activated with a fresh rate."));

    [HttpPost("configurations/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateConfiguration(Guid id, [FromBody] FxConfigurationRevisionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.DeactivateAsync(GetUserId(), id, request.Revision, ct), "FX provider configuration deactivated."));

    [HttpPost("configurations/{id:guid}/refresh")]
    public async Task<IActionResult> RefreshConfiguration(Guid id, [FromBody] FxConfigurationRevisionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _providers.RefreshAsync(GetUserId(), id, request.Revision, ct), "FX provider rate checked successfully."));

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetDashboardAsync(ct), "FX operations dashboard retrieved successfully."));

    [HttpGet("rates")]
    public async Task<IActionResult> Rates([FromQuery] string? sourceCurrencyCode, [FromQuery] string? destinationCurrencyCode, [FromQuery] bool? active, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetRatesAsync(sourceCurrencyCode, destinationCurrencyCode, active, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "FX rates retrieved successfully."));
    }

    [HttpPost("rates")]
    public async Task<IActionResult> CreateRate([FromBody] CreateManagedExchangeRateRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateManagedRateAsync(GetUserId(), request, ct), "FX rate created successfully."));

    [HttpPost("rates/{rateId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRate(Guid rateId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.DeactivateRateAsync(GetUserId(), rateId, ct), "FX rate deactivated successfully."));

    [HttpGet("markup-rules")]
    public async Task<IActionResult> MarkupRules([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetMarkupRulesAsync(page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "FX markup rules retrieved successfully."));
    }

    [HttpPost("markup-rules")]
    public async Task<IActionResult> UpsertMarkupRule([FromBody] UpsertFxMarkupRuleRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.UpsertMarkupRuleAsync(GetUserId(), request, ct), "FX markup rule saved successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
