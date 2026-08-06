using System.Security.Claims;
using KorridorX.Data;
using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/providers")]
public class AdminProviderRecoveryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBankDirectoryService _bankDirectoryService;
    private readonly IProviderOperationsQueryService _queryService;
    private readonly IProviderReconciliationService _reconciliationService;
    private readonly IRemittanceProvider _provider;
    private readonly IPayoutService _payoutService;

    public AdminProviderRecoveryController(
        AppDbContext db,
        IBankDirectoryService bankDirectoryService,
        IProviderOperationsQueryService queryService,
        IProviderReconciliationService reconciliationService,
        IRemittanceProvider provider,
        IPayoutService payoutService)
    {
        _db = db;
        _bankDirectoryService = bankDirectoryService;
        _queryService = queryService;
        _reconciliationService = reconciliationService;
        _provider = provider;
        _payoutService = payoutService;
    }

    [HttpPost("blaaiz/banks/sync")]
    public async Task<IActionResult> SyncBanks(
        [FromQuery] string? countryCode,
        [FromQuery] string? currencyCode,
        CancellationToken ct)
    {
        var result = await _bankDirectoryService.SyncAsync(countryCode, currencyCode, ct);
        return Ok(ApiResponses.Ok(result, "Provider bank directory synchronized successfully."));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? transactionType,
        [FromQuery] string? providerStatus,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetTransactionsAsync(
            transactionType,
            providerStatus,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Provider transactions retrieved successfully."));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetRequestLogsAsync(status, search, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Provider request logs retrieved successfully."));
    }

    [HttpGet("requests/{requestLogId:guid}")]
    public async Task<IActionResult> GetRequest(Guid requestLogId, CancellationToken ct)
    {
        var result = await _queryService.GetRequestLogAsync(requestLogId, ct);
        return Ok(ApiResponses.Ok(result, "Provider request log retrieved successfully."));
    }

    [HttpGet("webhooks")]
    public async Task<IActionResult> GetWebhooks(
        [FromQuery] string? processingStatus,
        [FromQuery] string? eventType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetWebhookEventsAsync(
            processingStatus,
            eventType,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Provider webhook events retrieved successfully."));
    }

    [HttpPost("transactions/{providerTransactionRowId:guid}/reconcile")]
    public async Task<IActionResult> ReconcileTransaction(
        Guid providerTransactionRowId,
        CancellationToken ct)
    {
        var result = await _reconciliationService.ReconcileOneAsync(providerTransactionRowId, ct);
        return Ok(ApiResponses.Ok(result, "Provider transaction reconciled successfully."));
    }

    [HttpPost("transactions/{providerTransactionRowId:guid}/webhook-replay")]
    public async Task<IActionResult> ReplayWebhook(
        Guid providerTransactionRowId,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == providerTransactionRowId && !x.IsDeleted, ct);

        if (transaction is null)
        {
            throw new InvalidOperationException("Provider transaction not found.");
        }

        if (!transaction.ProviderStatus.Equals("SUCCESSFUL", StringComparison.OrdinalIgnoreCase) &&
            !transaction.ProviderStatus.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Webhook replay is available only for successful provider transactions.");
        }

        var providerTransactionDate = transaction.ProviderCreatedAt ?? transaction.CreatedAt;
        if (providerTransactionDate < DateTime.UtcNow.AddDays(-90))
        {
            throw new InvalidOperationException(
                "The provider webhook replay retention window has expired for this transaction.");
        }

        var replay = await _provider.ReplayWebhookAsync(transaction.ProviderTransactionId, ct);
        var result = new ProviderWebhookReplayDto(
            replay.ProviderTransactionId,
            replay.Message,
            replay.ProviderRequestLogId);

        return Ok(ApiResponses.Ok(result, "Provider webhook replay requested successfully."));
    }

    [HttpPost("~/api/admin/payouts/{payoutId:guid}/retry")]
    public async Task<IActionResult> RetryPayout(Guid payoutId, CancellationToken ct)
    {
        var result = await _payoutService.RetryFailedAsync(payoutId, GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Failed payout retry completed."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
