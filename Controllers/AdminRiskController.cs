using System.Security.Claims;
using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KorridorX.Configuration;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/risk")]
public class AdminRiskController : ControllerBase
{
    private readonly ITransferRiskService _riskService;

    public AdminRiskController(ITransferRiskService riskService)
    {
        _riskService = riskService;
    }

    [HttpGet("flags")]
    public async Task<IActionResult> GetFlags(
        [FromQuery] bool? isResolved,
        [FromQuery] string? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _riskService.GetFlagsAsync(isResolved, severity, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Risk flags retrieved successfully."));
    }

    [HttpGet("flags/{flagId:guid}")]
    public async Task<IActionResult> GetFlag(Guid flagId, CancellationToken ct)
    {
        var result = await _riskService.GetFlagAsync(flagId, ct);
        return Ok(ApiResponses.Ok(result, "Risk flag retrieved successfully."));
    }

    [HttpPost("flags/{flagId:guid}/review")]
    public async Task<IActionResult> ReviewFlag(
        Guid flagId,
        [FromBody] ReviewAmlFlagRequestDto request,
        CancellationToken ct)
    {
        var result = await _riskService.ReviewFlagAsync(flagId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Risk flag review saved successfully."));
    }

    [HttpPost("transfers/{transferId:guid}/reassess")]
    public async Task<IActionResult> ReassessTransfer(
        Guid transferId,
        [FromBody] ReassessTransferRiskRequestDto request,
        CancellationToken ct)
    {
        var result = await _riskService.ReassessAsync(
            transferId,
            GetUserId(),
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer risk reassessment completed successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
