using System.Security.Claims;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/business/transfers")]
public class BusinessTransfersController : ControllerBase
{
    private readonly IBusinessTransferService _service;

    public BusinessTransfersController(IBusinessTransferService service)
    {
        _service = service;
    }

    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateBusinessTransferQuoteRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateQuoteAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer quote created successfully."));
    }

    [HttpGet("quotes/{quoteId:guid}")]
    public async Task<IActionResult> GetQuote(Guid quoteId, CancellationToken ct)
    {
        var result = await _service.GetQuoteAsync(GetUserId(), quoteId, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer quote retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateBusinessTransferRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateTransferAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer created successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetTransfersAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business transfers retrieved successfully."));
    }

    [HttpGet("{transferId:guid}")]
    public async Task<IActionResult> GetTransfer(Guid transferId, CancellationToken ct)
    {
        var result = await _service.GetTransferAsync(GetUserId(), transferId, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer retrieved successfully."));
    }

    [HttpPost("{transferId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid transferId,
        [FromBody] BusinessTransferDecisionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.ApproveAsync(GetUserId(), transferId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer approval recorded."));
    }

    [HttpPost("{transferId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid transferId,
        [FromBody] BusinessTransferDecisionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RejectAsync(GetUserId(), transferId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer rejected."));
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _service.GetReportAsync(GetUserId(), from, to, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer report retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
