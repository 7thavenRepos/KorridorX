using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/payouts")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutsController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
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
        CancellationToken ct)
    {
        var result = await _payoutService.DispatchForTransferAsync(
            transferId,
            "Admin",
            GetUserId(),
            ct);

        return Ok(ApiResponses.Ok(result, "Payout dispatch completed."));
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
