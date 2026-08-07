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

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/compliance/retention")]
public sealed class AdminDataRetentionController : ControllerBase
{
    private readonly IDataRetentionService _retention;

    public AdminDataRetentionController(IDataRetentionService retention)
    {
        _retention = retention;
    }

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies(
        [FromQuery] bool? isActive,
        [FromQuery] RetentionRecordType? recordType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _retention.GetPoliciesAsync(isActive, recordType, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Data retention policies retrieved successfully."));
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] UpsertRetentionPolicyRequestDto request,
        CancellationToken ct)
    {
        var result = await _retention.CreatePolicyAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Data retention policy created successfully."));
    }

    [HttpPut("policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy(
        Guid policyId,
        [FromBody] UpsertRetentionPolicyRequestDto request,
        CancellationToken ct)
    {
        var result = await _retention.UpdatePolicyAsync(policyId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Data retention policy updated successfully."));
    }

    [HttpPost("policies/{policyId:guid}/execute")]
    public async Task<IActionResult> ExecutePolicy(
        Guid policyId,
        [FromQuery] bool dryRun = true,
        CancellationToken ct = default)
    {
        var result = await _retention.ExecutePolicyAsync(policyId, dryRun, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, dryRun
            ? "Data retention dry run completed successfully."
            : "Data retention policy executed successfully."));
    }

    [HttpGet("legal-holds")]
    public async Task<IActionResult> GetLegalHolds(
        [FromQuery] LegalHoldStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _retention.GetLegalHoldsAsync(status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Legal holds retrieved successfully."));
    }

    [HttpPost("legal-holds")]
    public async Task<IActionResult> CreateLegalHold(
        [FromBody] CreateLegalHoldRequestDto request,
        CancellationToken ct)
    {
        var result = await _retention.CreateLegalHoldAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Legal hold created successfully."));
    }

    [HttpPost("legal-holds/{legalHoldId:guid}/release")]
    public async Task<IActionResult> ReleaseLegalHold(
        Guid legalHoldId,
        [FromBody] ReleaseLegalHoldRequestDto request,
        CancellationToken ct)
    {
        var result = await _retention.ReleaseLegalHoldAsync(legalHoldId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Legal hold released successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
