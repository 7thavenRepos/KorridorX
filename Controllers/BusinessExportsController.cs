using System.Security.Claims;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/business/exports")]
public class BusinessExportsController : ControllerBase
{
    private readonly IBusinessReportExportService _service;

    public BusinessExportsController(IBusinessReportExportService service)
    {
        _service = service;
    }

    [HttpGet("transfers.csv")]
    public async Task<IActionResult> ExportTransfers(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var content = await _service.ExportTransfersCsvAsync(GetUserId(), from, to, ct);
        return File(content, "text/csv", $"business-transfers-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("payment-batches/{batchId:guid}.csv")]
    public async Task<IActionResult> ExportBatch(Guid batchId, CancellationToken ct)
    {
        var content = await _service.ExportBatchCsvAsync(GetUserId(), batchId, ct);
        return File(content, "text/csv", $"business-batch-{batchId}.csv");
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
