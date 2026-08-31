using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Providers;
using KorridorX.Services.Audit;
using KorridorX.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/payouts")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;
    private readonly IAuditService _audit;

    public PayoutsController(IPayoutService payoutService, IAuditService audit)
    {
        _payoutService = payoutService;
        _audit = audit;
    }

    [HttpGet("{payoutId:guid}")]
    public async Task<IActionResult> GetPayoutById(
        [FromRoute] Guid payoutId,
        CancellationToken ct)
    {
        var result = await _payoutService.GetPayoutByIdAsync(GetUserId(), payoutId, ct);
        return Ok(ApiResponses.Ok(result, "Payout retrieved successfully."));
    }

    [HttpGet("~/api/transfers/{transferId:guid}/payout")]
    public async Task<IActionResult> GetTransferPayout(
        [FromRoute] Guid transferId,
        CancellationToken ct)
    {
        var result = await _payoutService.GetTransferPayoutAsync(GetUserId(), transferId, ct);
        return Ok(ApiResponses.Ok(result, "Transfer payout retrieved successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPayouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _payoutService.GetMyPayoutsAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Payouts retrieved successfully."));
    }

    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    [HttpPost("~/api/admin/transfers/{transferId:guid}/payout/dispatch")]
    public async Task<IActionResult> DispatchTransferPayout(
        [FromRoute] Guid transferId,
        [FromBody] ProviderAdminActionRequestDto request,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();
        var result = await _payoutService.DispatchForTransferAsync(
            transferId,
            "Admin",
            userId,
            ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "PAYOUT_DISPATCH_APPROVED",
                Category: "ProviderOperations",
                EntityName: "Transfer",
                EntityId: transferId.ToString(),
                NewValues: result,
                Metadata: new { Reason = reason },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(result, "Payout dispatch completed."));
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required for payout dispatch.");

        var cleaned = reason.Trim();
        if (cleaned.Length > 1000)
            throw new InvalidOperationException("Reason cannot exceed 1000 characters.");

        return cleaned;
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
