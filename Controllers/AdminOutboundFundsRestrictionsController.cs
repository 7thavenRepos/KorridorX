using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Compliance,InternalAudit,Operations,Admin,SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/outbound-funds-restrictions")]
public sealed class AdminOutboundFundsRestrictionsController
    : ControllerBase
{
    private readonly IOutboundFundsRestrictionService _service;

    public AdminOutboundFundsRestrictionsController(
        IOutboundFundsRestrictionService service)
    {
        _service = service;
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> SearchSubjects(
        [FromQuery] OutboundFundsRestrictionSubjectType subjectType,
        [FromQuery] string search,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.SearchSubjectsAsync(
                subjectType,
                search,
                take,
                ct),
            "Restriction subjects retrieved successfully."));

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] OutboundFundsRestrictionSubjectType? subjectType,
        [FromQuery] OutboundFundsRestrictionSource? source,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAsync(
            subjectType,
            source,
            isActive,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Outbound funds restrictions retrieved successfully."));
    }

    [HttpGet("{restrictionId:guid}")]
    public async Task<IActionResult> GetById(
        Guid restrictionId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetByIdAsync(restrictionId, ct),
            "Outbound funds restriction retrieved successfully."));

    [Authorize(Roles = "Compliance,InternalAudit,Admin,SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyOutboundFundsRestrictionRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.ApplyAsync(
                request,
                GetUserId(),
                ct),
            "Outbound funds restriction applied successfully."));

    [Authorize(Roles = "Compliance,InternalAudit,Admin,SuperAdmin")]
    [HttpPost("{restrictionId:guid}/lift")]
    public async Task<IActionResult> Lift(
        Guid restrictionId,
        [FromBody] LiftOutboundFundsRestrictionRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.LiftAsync(
                restrictionId,
                request,
                GetUserId(),
                ct),
            "Outbound funds restriction released successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Invalid authenticated user.");
    }
}
