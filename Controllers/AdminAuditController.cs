using KorridorX.Configuration;
using KorridorX.Infrastructure;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance,InternalAudit,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AdminAuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _auditService.GetAsync(
            category,
            action,
            entityName,
            entityId,
            userId,
            correlationId,
            search,
            from,
            to,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Audit logs retrieved successfully."));
    }

    [HttpGet("{auditLogId:guid}")]
    public async Task<IActionResult> GetById(
        Guid auditLogId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _auditService.GetByIdAsync(auditLogId, ct),
            "Audit log retrieved successfully."));
}