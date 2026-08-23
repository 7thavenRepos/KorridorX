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

    [HttpGet("liquidity/positions")]
    public async Task<IActionResult> LiquidityPositions(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetLiquidityPositionsAsync(ct),
            "Unified treasury liquidity positions retrieved successfully."));

    [HttpGet("liquidity/exposure")]
    public async Task<IActionResult> AssetExposure(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetAssetExposureAsync(ct),
            "Treasury asset exposure retrieved successfully."));

    [HttpGet("liquidity/alerts")]
    public async Task<IActionResult> LiquidityAlerts(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetLiquidityAlertsAsync(ct),
            "Treasury liquidity alerts retrieved successfully."));

    [HttpGet("liquidity/rebalancing-suggestions")]
    public async Task<IActionResult> UnifiedRebalanceSuggestions(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetUnifiedRebalanceSuggestionsAsync(ct),
            "Unified treasury rebalancing suggestions retrieved successfully."));

    [HttpPost("liquidity/internal-transfer")]
    public async Task<IActionResult> InternalLiquidityTransfer(
        [FromBody] ExecuteInternalLiquidityTransferRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.ExecuteInternalLiquidityTransferAsync(GetUserId(), request, ct),
            "Internal treasury liquidity transfer completed successfully."));

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


    [HttpGet("rebalancing/suggestions")]
    public async Task<IActionResult> RebalanceSuggestions(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetRebalanceSuggestionsAsync(ct), "Treasury rebalance suggestions retrieved successfully."));

    [HttpGet("rebalancing")]
    public async Task<IActionResult> Rebalances([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetRebalancesAsync(page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Treasury rebalances retrieved successfully."));
    }

    [HttpPost("rebalancing")]
    public async Task<IActionResult> CreateRebalance([FromBody] CreateTreasuryRebalanceRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateRebalanceAsync(GetUserId(), request, ct), "Treasury rebalance request created successfully."));

    [HttpPost("rebalancing/{rebalanceId:guid}/review")]
    public async Task<IActionResult> ReviewRebalance(Guid rebalanceId, [FromBody] ReviewTreasuryRebalanceRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.ReviewRebalanceAsync(GetUserId(), rebalanceId, request, ct), request.Approve ? "Treasury rebalance approved successfully." : "Treasury rebalance rejected successfully."));

    [HttpPost("rebalancing/{rebalanceId:guid}/execute")]
    public async Task<IActionResult> ExecuteRebalance(Guid rebalanceId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.ExecuteRebalanceAsync(GetUserId(), rebalanceId, ct), "Treasury rebalance executed successfully."));

    [HttpPost("settlements/{batchId:guid}/statement")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportStatement(Guid batchId, [FromForm] ImportSettlementStatementFormDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.ImportSettlementStatementAsync(GetUserId(), batchId, request, ct), "Settlement statement imported successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
