using KorridorX.Configuration;
using KorridorX.Infrastructure;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/compliance/operations")]
public sealed class AdminComplianceOperationsController : ControllerBase
{
    private readonly IComplianceOperationsQueryService _service;

    public AdminComplianceOperationsController(
        IComplianceOperationsQueryService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken ct = default)
    {
        var result = await _service.GetSummaryAsync(ct);

        return Ok(ApiResponses.Ok(
            result,
            "Compliance operations summary retrieved successfully."));
    }
}