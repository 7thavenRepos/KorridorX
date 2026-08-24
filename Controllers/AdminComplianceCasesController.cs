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
[Route("api/admin/compliance")]
public sealed class AdminComplianceCasesController : ControllerBase
{
    private readonly IComplianceCaseService _caseService;
    private readonly IComplianceScreeningService _screeningService;

    public AdminComplianceCasesController(
        IComplianceCaseService caseService,
        IComplianceScreeningService screeningService)
    {
        _caseService = caseService;
        _screeningService = screeningService;
    }

    [HttpGet("cases")]
    public async Task<IActionResult> GetCases(
        [FromQuery] ComplianceCaseStatus? status,
        [FromQuery] ComplianceCaseType? caseType,
        [FromQuery] ComplianceCasePriority? priority,
        [FromQuery] bool? isBlocking,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _caseService.GetCasesAsync(
            status,
            caseType,
            priority,
            isBlocking,
            assignedToUserId,
            search,
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Compliance cases retrieved successfully."));
    }

    [HttpGet("cases/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _caseService.GetSummaryAsync(ct);
        return Ok(ApiResponses.Ok(result, "Compliance case summary retrieved successfully."));
    }

    [HttpGet("cases/{caseId:guid}")]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken ct)
    {
        var result = await _caseService.GetCaseAsync(caseId, ct);
        return Ok(ApiResponses.Ok(result, "Compliance case retrieved successfully."));
    }

    [HttpPost("cases/{caseId:guid}/assign")]
    public async Task<IActionResult> Assign(
        Guid caseId,
        [FromBody] AssignComplianceCaseRequestDto request,
        CancellationToken ct)
    {
        var result = await _caseService.AssignAsync(caseId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Compliance case assignment updated successfully."));
    }

    [HttpPost("cases/{caseId:guid}/notes")]
    public async Task<IActionResult> AddNote(
        Guid caseId,
        [FromBody] AddComplianceCaseNoteRequestDto request,
        CancellationToken ct)
    {
        var result = await _caseService.AddNoteAsync(caseId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Compliance case note added successfully."));
    }

    [HttpPost("cases/{caseId:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(
        Guid caseId,
        [FromBody] AddComplianceCaseEvidenceRequestDto request,
        CancellationToken ct)
    {
        var result = await _caseService.AddEvidenceAsync(caseId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Compliance case evidence added successfully."));
    }

    [HttpPost("cases/{caseId:guid}/decision")]
    public async Task<IActionResult> Decide(
        Guid caseId,
        [FromBody] DecideComplianceCaseRequestDto request,
        CancellationToken ct)
    {
        var result = await _caseService.DecideAsync(caseId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Compliance case decision saved successfully."));
    }

    [HttpGet("screenings")]
    public async Task<IActionResult> GetScreenings(
        [FromQuery] ScreeningSubjectType? subjectType,
        [FromQuery] ScreeningStatus? status,
        [FromQuery] bool? isBlocking,
        [FromQuery] Guid? transferId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _screeningService.GetScreeningsAsync(
            subjectType,
            status,
            isBlocking,
            transferId,
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Screening records retrieved successfully."));
    }

    [HttpPost("screenings/run")]
    public async Task<IActionResult> RunScreening(
        [FromBody] ManualScreeningRequestDto request,
        CancellationToken ct)
    {
        var result = await _screeningService.ManualScreenAsync(
            request.SubjectType,
            request.SubjectId,
            request.TransferId,
            GetUserId(),
            ct);
        return Ok(ApiResponses.Ok(result, "Compliance screening completed successfully."));
    }

    [HttpPost("screenings/rescreen-due")]
    public async Task<IActionResult> RescreenDue(
        [FromBody] RunDueRescreeningRequestDto request,
        CancellationToken ct = default)
    {
        var count = await _screeningService.RunDueRescreeningAsync(
            request.BatchSize,
            GetUserId(),
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            new { Processed = count },
            "Due screening subjects processed successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
