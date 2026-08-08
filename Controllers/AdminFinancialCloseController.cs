using System.Security.Claims;
using KorridorX.Dtos.Finance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/finance")]
public sealed class AdminFinancialCloseController : ControllerBase
{
    private readonly IFinancialCloseService _service;
    public AdminFinancialCloseController(IFinancialCloseService service) => _service = service;

    [HttpGet("statements/income")]
    public async Task<IActionResult> Income([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? baseCurrencyCode, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetIncomeStatementAsync(from, to, baseCurrencyCode, ct), "Income statement retrieved successfully."));

    [HttpGet("statements/balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateTime asOf, [FromQuery] string? baseCurrencyCode, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetBalanceSheetAsync(asOf, baseCurrencyCode, ct), "Balance sheet retrieved successfully."));

    [HttpGet("translation-rates")]
    public async Task<IActionResult> TranslationRates([FromQuery] string? sourceCurrencyCode, [FromQuery] string? baseCurrencyCode, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetTranslationRatesAsync(sourceCurrencyCode, baseCurrencyCode, ct), "Finance translation rates retrieved successfully."));

    [HttpPost("translation-rates")]
    public async Task<IActionResult> TranslationRate([FromBody] UpsertFinanceTranslationRateRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpsertTranslationRateAsync(UserId(), request, ct), "Finance translation rate created successfully."));

    [HttpGet("provider-invoices")]
    public async Task<IActionResult> ProviderInvoices([FromQuery] ProviderInvoiceStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) { var r = await _service.GetProviderInvoicesAsync(status, page, pageSize, ct); return Ok(ApiResponses.OkPaged(r.Items, r.Meta, "Provider invoices retrieved successfully.")); }

    [HttpGet("provider-invoices/{invoiceId:guid}")]
    public async Task<IActionResult> ProviderInvoice(Guid invoiceId, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetProviderInvoiceAsync(invoiceId, ct), "Provider invoice retrieved successfully."));

    [HttpPost("provider-invoices/import")]
    public async Task<IActionResult> ImportProviderInvoice([FromForm] ImportProviderInvoiceFormDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ImportProviderInvoiceAsync(UserId(), request, ct), "Provider invoice imported successfully."));

    [HttpPost("provider-invoices/{invoiceId:guid}/review")]
    public async Task<IActionResult> ReviewProviderInvoice(Guid invoiceId, [FromBody] ReviewProviderInvoiceRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ReviewProviderInvoiceAsync(UserId(), invoiceId, request, ct), request.Approve ? "Provider invoice approved and posted successfully." : "Provider invoice rejected successfully."));

    [HttpGet("tax-rules")]
    public async Task<IActionResult> TaxRules(CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetTaxRulesAsync(ct), "Tax rules retrieved successfully."));

    [HttpPost("tax-rules")]
    public async Task<IActionResult> CreateTaxRule([FromBody] UpsertTaxRuleRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpsertTaxRuleAsync(UserId(), null, request, ct), "Tax rule created successfully."));

    [HttpPut("tax-rules/{taxRuleId:guid}")]
    public async Task<IActionResult> UpdateTaxRule(Guid taxRuleId, [FromBody] UpsertTaxRuleRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpsertTaxRuleAsync(UserId(), taxRuleId, request, ct), "Tax rule updated successfully."));

    [HttpPost("tax/calculate")]
    public async Task<IActionResult> CalculateTax([FromBody] CalculateTaxRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CalculateTaxAsync(request, ct), "Tax calculated successfully."));

    [HttpPost("accruals")]
    public async Task<IActionResult> CreateAccrual([FromBody] CreateAccrualRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.CreateAccrualAsync(UserId(), request, ct), "Accrual posted successfully."));

    [HttpGet("close/{periodId:guid}/checklist")]
    public async Task<IActionResult> Checklist(Guid periodId, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetCloseChecklistAsync(periodId, ct), "Finance close checklist retrieved successfully."));

    [HttpPut("close/{periodId:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> UpdateChecklist(Guid periodId, Guid itemId, [FromBody] UpdateFinanceCloseChecklistItemRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.UpdateChecklistItemAsync(UserId(), periodId, itemId, request, ct), "Finance close checklist item updated successfully."));

    [HttpGet("close/requests")]
    public async Task<IActionResult> CloseRequests([FromQuery] Guid? periodId, [FromQuery] FinanceCloseRequestStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) { var r = await _service.GetCloseRequestsAsync(periodId, status, page, pageSize, ct); return Ok(ApiResponses.OkPaged(r.Items, r.Meta, "Finance close requests retrieved successfully.")); }

    [HttpPost("close/{periodId:guid}/requests")]
    public async Task<IActionResult> RequestClose(Guid periodId, [FromBody] CreateFinanceCloseRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.SubmitCloseRequestAsync(UserId(), periodId, request, ct), "Finance close request submitted successfully."));

    [HttpPost("close/requests/{closeRequestId:guid}/review")]
    public async Task<IActionResult> ReviewClose(Guid closeRequestId, [FromBody] ReviewFinanceCloseRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.ReviewCloseRequestAsync(UserId(), closeRequestId, request, ct), request.Approve ? "Finance close request approved and period closed successfully." : "Finance close request rejected successfully."));

    [HttpGet("close/{periodId:guid}/pack")]
    public async Task<IActionResult> ClosePack(Guid periodId, [FromQuery] string? baseCurrencyCode, CancellationToken ct) => Ok(ApiResponses.Ok(await _service.GetMonthlyClosePackAsync(periodId, baseCurrencyCode, ct), "Finance close pack retrieved successfully."));

    private Guid UserId() { var value = User.FindFirstValue(ClaimTypes.NameIdentifier); return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user."); }
}
