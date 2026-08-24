using System.Security.Claims;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Services.Audit;
using KorridorX.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/providers")]
public class AdminProviderOperationsController : ControllerBase
{
    private readonly IProviderReconciliationService _reconciliationService;
    private readonly IAuditService _audit;

    public AdminProviderOperationsController(
        IProviderReconciliationService reconciliationService,
        IAuditService audit)
    {
        _reconciliationService = reconciliationService;
        _audit = audit;
    }

    [HttpPost("blaaiz/reconcile")]
    public async Task<IActionResult> ReconcileBlaaiz(
        [FromBody] ProviderAdminActionRequestDto request,
        [FromQuery] int batchSize = 50,
        CancellationToken ct = default)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();

        var result = await _reconciliationService.ReconcilePendingAsync(batchSize, ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "PROVIDER_RECONCILIATION_BATCH_RUN",
                Category: "ProviderOperations",
                EntityName: "Provider",
                EntityId: "Blaaiz",
                NewValues: new
                {
                    BatchSize = Math.Clamp(batchSize, 1, 500),
                    result.Examined,
                    result.Updated,
                    result.Failed
                },
                Metadata: new
                {
                    Reason = reason,
                    Provider = "Blaaiz"
                },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(result, "Blaaiz reconciliation completed."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required for this provider operation.");

        var cleaned = reason.Trim();

        if (cleaned.Length > 1000)
            throw new InvalidOperationException("Reason cannot exceed 1000 characters.");

        return cleaned;
    }
}