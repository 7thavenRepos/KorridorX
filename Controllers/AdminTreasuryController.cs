using System.Security.Claims;
using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;
using KorridorX.Services.Treasury;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/treasury")]
public sealed class AdminTreasuryController : ControllerBase
{
    private readonly ITreasuryService _service;
    public AdminTreasuryController(ITreasuryService service) => _service = service;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetDashboardAsync(ct), "Treasury dashboard retrieved successfully."));

    [HttpPost("provider-wallets/sync")]
    public async Task<IActionResult> SyncProviderWallets(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.SyncProviderWalletsAsync(GetUserId(), ct), "Provider wallets synchronized successfully."));

    [HttpGet("provider-wallets")]
    public async Task<IActionResult> ProviderWallets([FromQuery] string? currencyCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetProviderWalletsAsync(currencyCode, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Provider wallets retrieved successfully."));
    }

    [HttpGet("liquidity-thresholds")]
    public async Task<IActionResult> Thresholds([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetThresholdsAsync(page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Liquidity thresholds retrieved successfully."));
    }

    [HttpPost("liquidity-thresholds")]
    public async Task<IActionResult> UpsertThreshold([FromBody] UpsertLiquidityThresholdRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.UpsertThresholdAsync(GetUserId(), request, ct), "Liquidity threshold saved successfully."));

    [HttpGet("settlements")]
    public async Task<IActionResult> Settlements([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetSettlementBatchesAsync(page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Settlement batches retrieved successfully."));
    }

    [HttpPost("settlements")]
    public async Task<IActionResult> CreateSettlement([FromBody] CreateSettlementBatchRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateSettlementBatchAsync(GetUserId(), request, ct), "Settlement batch created successfully."));

    [HttpPost("settlements/{batchId:guid}/reconcile")]
    public async Task<IActionResult> ReconcileSettlement(Guid batchId, [FromBody] ReconcileSettlementBatchRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.ReconcileSettlementBatchAsync(GetUserId(), batchId, request, ct), "Settlement batch reconciled successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
