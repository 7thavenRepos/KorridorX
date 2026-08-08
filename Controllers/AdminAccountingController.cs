using System.Security.Claims;
using KorridorX.Dtos.Finance;
using KorridorX.Infrastructure;
using KorridorX.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/finance/accounting")]
public sealed class AdminAccountingController : ControllerBase
{
    private readonly IAccountingService _service;
    public AdminAccountingController(IAccountingService service) => _service = service;

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetAccountsAsync(ct), "Accounting accounts retrieved successfully."));

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountingAccountRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CreateAccountAsync(UserId(), request, ct), "Accounting account created successfully."));

    [HttpGet("periods")]
    public async Task<IActionResult> Periods(CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetPeriodsAsync(ct), "Accounting periods retrieved successfully."));

    [HttpPost("periods")]
    public async Task<IActionResult> CreatePeriod([FromBody] CreateAccountingPeriodRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CreatePeriodAsync(UserId(), request, ct), "Accounting period created successfully."));

    [HttpPost("periods/{periodId:guid}/close")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Close(Guid periodId, [FromBody] CloseAccountingPeriodRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ClosePeriodAsync(UserId(), periodId, request, ct), "Accounting period closed successfully."));

    [HttpPost("periods/{periodId:guid}/reopen")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Reopen(Guid periodId, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ReopenPeriodAsync(UserId(), periodId, ct), "Accounting period reopened successfully."));

    [HttpPost("journals/manual")]
    public async Task<IActionResult> ManualJournal([FromBody] CreateManualJournalRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CreateManualJournalAsync(UserId(), request, ct), "Manual journal posted successfully."));

    [HttpPost("journals/{journalEntryId:guid}/reverse")]
    public async Task<IActionResult> ReverseJournal(Guid journalEntryId, [FromBody] ReverseJournalRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.ReverseJournalAsync(UserId(), journalEntryId, request, ct), "Journal entry reversed successfully."));

    [HttpGet("journals")]
    public async Task<IActionResult> Journals([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _service.GetJournalsAsync(from, to, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Accounting journals retrieved successfully."));
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetTrialBalanceAsync(from, to, ct), "Trial balance retrieved successfully."));

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.SyncAsync(from, to, ct), "Accounting synchronization completed."));

    [HttpGet("trial-balance.csv")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var bytes = await _service.ExportJournalsCsvAsync(from, to, ct);
        return File(bytes, "text/csv", $"korridorx-trial-balance-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private Guid UserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
