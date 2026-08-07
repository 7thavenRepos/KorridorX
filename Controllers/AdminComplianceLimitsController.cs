using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/compliance/limits")]
public class AdminComplianceLimitsController : ControllerBase
{
    private readonly IComplianceLimitService _service;

    public AdminComplianceLimitsController(IComplianceLimitService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? countryCode,
        [FromQuery] string? currencyCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetLimitsAsync(countryCode, currencyCode, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Compliance limits retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertComplianceLimitRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpsertAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Compliance limit saved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
