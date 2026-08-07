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
public sealed class AdminRegulatoryReportsController : ControllerBase
{
    private readonly IRegulatoryReportingService _reports;
    private readonly IComplianceManagementReportService _managementReport;

    public AdminRegulatoryReportsController(
        IRegulatoryReportingService reports,
        IComplianceManagementReportService managementReport)
    {
        _reports = reports;
        _managementReport = managementReport;
    }

    [HttpPost("cases/{caseId:guid}/regulatory-reports")]
    public async Task<IActionResult> Create(
        Guid caseId,
        [FromBody] CreateRegulatoryReportRequestDto request,
        CancellationToken ct)
    {
        var result = await _reports.CreateAsync(caseId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report draft created successfully."));
    }

    [HttpGet("regulatory-reports")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] RegulatoryReportStatus? status,
        [FromQuery] RegulatoryReportType? reportType,
        [FromQuery] string? jurisdictionCode,
        [FromQuery] Guid? complianceCaseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _reports.GetPagedAsync(
            status,
            reportType,
            jurisdictionCode,
            complianceCaseId,
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Regulatory reports retrieved successfully."));
    }

    [HttpGet("regulatory-reports/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _reports.GetSummaryAsync(ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report summary retrieved successfully."));
    }

    [HttpGet("management-summary")]
    public async Task<IActionResult> GetManagementSummary(CancellationToken ct)
    {
        var result = await _managementReport.GetSummaryAsync(ct);
        return Ok(ApiResponses.Ok(result, "Compliance management summary retrieved successfully."));
    }

    [HttpGet("regulatory-reports/{reportId:guid}")]
    public async Task<IActionResult> Get(Guid reportId, CancellationToken ct)
    {
        var result = await _reports.GetAsync(reportId, ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report retrieved successfully."));
    }

    [HttpPut("regulatory-reports/{reportId:guid}")]
    public async Task<IActionResult> Update(
        Guid reportId,
        [FromBody] UpdateRegulatoryReportRequestDto request,
        CancellationToken ct)
    {
        var result = await _reports.UpdateAsync(reportId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report updated successfully."));
    }

    [HttpPost("regulatory-reports/{reportId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid reportId, CancellationToken ct)
    {
        var result = await _reports.SubmitForApprovalAsync(reportId, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report submitted for approval successfully."));
    }

    [HttpPost("regulatory-reports/{reportId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid reportId,
        [FromBody] ReviewRegulatoryReportRequestDto request,
        CancellationToken ct)
    {
        var result = await _reports.ReviewAsync(reportId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report review completed successfully."));
    }

    [HttpPost("regulatory-reports/{reportId:guid}/filed")]
    public async Task<IActionResult> MarkFiled(
        Guid reportId,
        [FromBody] MarkRegulatoryReportFiledRequestDto request,
        CancellationToken ct)
    {
        var result = await _reports.MarkFiledAsync(reportId, GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Regulatory report marked as filed successfully."));
    }

    [HttpGet("regulatory-reports/{reportId:guid}/export")]
    public async Task<IActionResult> Export(Guid reportId, CancellationToken ct)
    {
        var result = await _reports.ExportPackageAsync(reportId, GetUserId(), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
