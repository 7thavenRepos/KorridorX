using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Support;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Support,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/support")]
public sealed class AdminSupportController : ControllerBase
{
    private readonly IAdminSupportService _service;
    public AdminSupportController(IAdminSupportService service) => _service = service;

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets([FromQuery] SupportTicketStatus? status, [FromQuery] SupportTicketPriority? priority, [FromQuery] Guid? assignedToUserId, [FromQuery] bool? slaBreached, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetTicketsAsync(status, priority, assignedToUserId, slaBreached, search, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Support tickets retrieved successfully."));
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetTicketAsync(ticketId, ct), "Support ticket retrieved successfully."));

    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> Reply(Guid ticketId, [FromBody] AdminAddSupportMessageRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ReplyAsync(GetUserId(), ticketId, request, ct), "Support reply added successfully."));

    [HttpPut("tickets/{ticketId:guid}")]
    public async Task<IActionResult> UpdateTicket(Guid ticketId, [FromBody] UpdateSupportTicketRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpdateTicketAsync(GetUserId(), ticketId, request, ct), "Support ticket updated successfully."));

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes([FromQuery] TransferDisputeStatus? status, [FromQuery] Guid? transferId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetDisputesAsync(status, transferId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Transfer disputes retrieved successfully."));
    }

    [HttpPost("disputes/{disputeId:guid}/resolve")]
    public async Task<IActionResult> ResolveDispute(Guid disputeId, [FromBody] ResolveTransferDisputeRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ResolveDisputeAsync(GetUserId(), disputeId, request, ct), "Transfer dispute updated successfully."));

    [HttpPost("transfers/{transferId:guid}/investigations")]
    public async Task<IActionResult> CreateInvestigation(Guid transferId, [FromBody] CreateTransferInvestigationRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CreateInvestigationAsync(GetUserId(), transferId, request, ct), "Transfer investigation opened successfully."));

    [HttpGet("investigations")]
    public async Task<IActionResult> GetInvestigations([FromQuery] TransferInvestigationStatus? status, [FromQuery] Guid? assignedToUserId, [FromQuery] bool? overdue, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetInvestigationsAsync(status, assignedToUserId, overdue, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Transfer investigations retrieved successfully."));
    }

    [HttpPut("investigations/{investigationId:guid}")]
    public async Task<IActionResult> UpdateInvestigation(Guid investigationId, [FromBody] UpdateTransferInvestigationRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpdateInvestigationAsync(GetUserId(), investigationId, request, ct), "Transfer investigation updated successfully."));

    [HttpPost("investigations/{investigationId:guid}/evidence")]
    public async Task<IActionResult> AddInvestigationEvidence(Guid investigationId, [FromBody] AddSupportEvidenceRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.AddInvestigationEvidenceAsync(GetUserId(), investigationId, request, ct), "Investigation evidence added successfully."));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetSummaryAsync(ct), "Support operations summary retrieved successfully."));

    [HttpPost("sla/process")]
    public async Task<IActionResult> ProcessSla([FromQuery] int batchSize = 100, CancellationToken ct = default) => Ok(ApiResponses.Ok(new { Processed = await _service.ProcessSlaBreachesAsync(batchSize, ct) }, "Support SLA processing completed successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
