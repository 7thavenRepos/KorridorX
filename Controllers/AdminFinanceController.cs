using KorridorX.Infrastructure;
using KorridorX.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/finance")]
public sealed class AdminFinanceController : ControllerBase
{
    private readonly IFinanceReportingService _service;
    public AdminFinanceController(IFinanceReportingService service) => _service = service;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetSummaryAsync(from, to, ct), "Finance summary retrieved successfully."));

    [HttpGet("corridors.csv")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(from, to, ct);
        return File(bytes, "text/csv", $"korridorx-finance-corridors-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
