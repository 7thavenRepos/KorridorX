using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/collections/{collectionId:guid}/refund")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundsController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRefund(Guid collectionId, CancellationToken ct)
    {
        var result = await _refundService.GetAsync(collectionId, ct);
        return Ok(ApiResponses.Ok(result, "Refund retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> InitiateRefund(
        Guid collectionId,
        InitiateRefundRequestDto request,
        CancellationToken ct)
    {
        var result = await _refundService.InitiateAsync(collectionId, request.Reason, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Refund initiation completed."));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshRefund(Guid collectionId, CancellationToken ct)
    {
        var result = await _refundService.RefreshAsync(collectionId, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Refund status refreshed successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
