using System.Security.Claims;
using KorridorX.Dtos.Treasury;
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
    public AdminFxOperationsController(IFxOperationsService service) => _service = service;

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
