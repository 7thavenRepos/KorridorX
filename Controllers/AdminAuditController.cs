using KorridorX.Infrastructure;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance,Operations")]
[ApiController]
[Route("api/admin/audit-logs")]
public class AdminAuditController : ControllerBase
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
        [FromQuery] Guid? userId = null,
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
            userId,
            from,
            to,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Audit logs retrieved successfully."));
    }
}
