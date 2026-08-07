using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Support;
using KorridorX.Infrastructure;
using KorridorX.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/support")]
public sealed class SupportController : ControllerBase
{
    private readonly ISupportService _service;
    public SupportController(ISupportService service) => _service = service;

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateTicketAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Support ticket created successfully."));
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetMyTicketsAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Support tickets retrieved successfully."));
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId, CancellationToken ct)
    {
        var result = await _service.GetMyTicketAsync(GetUserId(), ticketId, ct);
        return Ok(ApiResponses.Ok(result, "Support ticket retrieved successfully."));
    }

    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> Reply(Guid ticketId, [FromBody] AddSupportMessageRequestDto request, CancellationToken ct)
    {
        var result = await _service.AddCustomerMessageAsync(GetUserId(), ticketId, request, ct);
        return Ok(ApiResponses.Ok(result, "Support message added successfully."));
    }

    [HttpPost("tickets/{ticketId:guid}/evidence")]
    public async Task<IActionResult> AddTicketEvidence(Guid ticketId, [FromBody] AddSupportEvidenceRequestDto request, CancellationToken ct)
    {
        var result = await _service.AddTicketEvidenceAsync(GetUserId(), ticketId, request, ct);
        return Ok(ApiResponses.Ok(result, "Support evidence added successfully."));
    }

    [HttpPost("transfers/{transferId:guid}/disputes")]
    public async Task<IActionResult> CreateDispute(Guid transferId, [FromBody] CreateTransferDisputeRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateDisputeAsync(GetUserId(), transferId, request, ct);
        return Ok(ApiResponses.Ok(result, "Transfer dispute opened successfully."));
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetMyDisputesAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Transfer disputes retrieved successfully."));
    }

    [HttpPost("disputes/{disputeId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawDispute(Guid disputeId, CancellationToken ct)
    {
        var result = await _service.WithdrawDisputeAsync(GetUserId(), disputeId, ct);
        return Ok(ApiResponses.Ok(result, "Transfer dispute withdrawn successfully."));
    }

    [HttpPost("disputes/{disputeId:guid}/evidence")]
    public async Task<IActionResult> AddDisputeEvidence(Guid disputeId, [FromBody] AddSupportEvidenceRequestDto request, CancellationToken ct)
    {
        var result = await _service.AddDisputeEvidenceAsync(GetUserId(), disputeId, request, ct);
        return Ok(ApiResponses.Ok(result, "Dispute evidence added successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
