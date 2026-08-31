using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Providers;
using KorridorX.Services.Audit;
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
    private readonly IAuditService _audit;

    public RefundsController(IRefundService refundService, IAuditService audit)
    {
        _refundService = refundService;
        _audit = audit;
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
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();
        var result = await _refundService.InitiateAsync(collectionId, reason, userId, ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "COLLECTION_REFUND_APPROVED",
                Category: "ProviderOperations",
                EntityName: "Collection",
                EntityId: collectionId.ToString(),
                NewValues: result,
                Metadata: new { Reason = reason },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(result, "Refund initiation completed."));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshRefund(
        Guid collectionId,
        [FromBody] ProviderAdminActionRequestDto request,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();
        var result = await _refundService.RefreshAsync(collectionId, userId, ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "COLLECTION_REFUND_REFRESHED",
                Category: "ProviderOperations",
                EntityName: "Collection",
                EntityId: collectionId.ToString(),
                NewValues: result,
                Metadata: new { Reason = reason },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(result, "Refund status refreshed successfully."));
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required for this refund operation.");

        var cleaned = reason.Trim();
        if (cleaned.Length > 1000)
            throw new InvalidOperationException("Reason cannot exceed 1000 characters.");

        return cleaned;
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
