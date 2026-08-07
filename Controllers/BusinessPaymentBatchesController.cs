using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/business/payment-batches")]
public class BusinessPaymentBatchesController : ControllerBase
{
    private readonly IBusinessPaymentBatchService _service;

    public BusinessPaymentBatchesController(IBusinessPaymentBatchService service)
    {
        _service = service;
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        [FromForm] ImportBusinessPaymentBatchFormDto request,
        CancellationToken ct = default)
    {
        if (request.File is null || request.File.Length == 0)
            throw new InvalidOperationException("A non-empty CSV file is required.");

        await using var stream = request.File.OpenReadStream();
        var result = await _service.ImportAsync(
            GetUserId(),
            request.File.FileName,
            stream,
            new ImportBusinessPaymentBatchRequestDto(
                request.Name,
                request.SourceCountryCode,
                request.SourceCurrencyCode,
                request.FundingSource),
            ct);

        return Ok(ApiResponses.Ok(result, "Business payment batch imported and validated."));
    }

    [HttpGet]
    public async Task<IActionResult> GetBatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetBatchesAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business payment batches retrieved successfully."));
    }

    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetBatch(Guid batchId, CancellationToken ct)
    {
        var result = await _service.GetBatchAsync(GetUserId(), batchId, ct);
        return Ok(ApiResponses.Ok(result, "Business payment batch retrieved successfully."));
    }

    [HttpPost("{batchId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid batchId, CancellationToken ct)
    {
        var result = await _service.SubmitAsync(GetUserId(), batchId, ct);
        return Ok(ApiResponses.Ok(result, "Business payment batch submitted successfully."));
    }

    [HttpPost("{batchId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid batchId,
        [FromBody] BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.ApproveAsync(GetUserId(), batchId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business payment batch approval recorded."));
    }

    [HttpPost("{batchId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid batchId,
        [FromBody] BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RejectAsync(GetUserId(), batchId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business payment batch rejected."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
