using System.Security.Claims;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Audit;
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
    private readonly IAuditService _audit;

    public AdminProviderRecoveryController(
        AppDbContext db,
        IBankDirectoryService bankDirectoryService,
        IProviderOperationsQueryService queryService,
        IProviderReconciliationService reconciliationService,
        IRemittanceProvider provider,
        IPayoutService payoutService,
        IAuditService audit)
    {
        _db = db;
        _bankDirectoryService = bankDirectoryService;
        _queryService = queryService;
        _reconciliationService = reconciliationService;
        _provider = provider;
        _payoutService = payoutService;
        _audit = audit;
    }

    [HttpGet("blaaiz/banks")]
    public async Task<IActionResult> GetBanks(
        [FromQuery] string? countryCode,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var result = await _bankDirectoryService.GetBanksAsync(
            countryCode,
            search,
            includeInactive,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Provider bank directory retrieved successfully."));
    }

    [HttpPost("blaaiz/banks/sync")]
    public async Task<IActionResult> SyncBanks(
        [FromBody] ProviderAdminActionRequestDto request,
        [FromQuery] string? countryCode,
        [FromQuery] string? currencyCode,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();

        var result = await _bankDirectoryService.SyncAsync(
            countryCode,
            currencyCode,
            ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "PROVIDER_BANK_DIRECTORY_SYNCED",
                Category: "ProviderOperations",
                EntityName: "ProviderBankDirectory",
                EntityId: _provider.ProviderCode.ToString(),
                NewValues: result,
                Metadata: new
                {
                    Reason = reason,
                    CountryCode = NormalizeOptionalCode(countryCode),
                    CurrencyCode = NormalizeOptionalCode(currencyCode),
                    Provider = _provider.ProviderName
                },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider bank directory synchronized successfully."));
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

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Provider transactions retrieved successfully."));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetRequestLogsAsync(
            status,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Provider request logs retrieved successfully."));
    }

    [HttpGet("requests/{requestLogId:guid}")]
    public async Task<IActionResult> GetRequest(
        Guid requestLogId,
        CancellationToken ct)
    {
        var result = await _queryService.GetRequestLogAsync(requestLogId, ct);
        return Ok(ApiResponses.Ok(
            result,
            "Provider request log retrieved successfully."));
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

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Provider webhook events retrieved successfully."));
    }

    [HttpGet("webhooks/{webhookEventId:guid}")]
    public async Task<IActionResult> GetWebhook(
        Guid webhookEventId,
        CancellationToken ct)
    {
        var result = await _queryService.GetWebhookEventAsync(
            webhookEventId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider webhook event retrieved successfully."));
    }

    [HttpGet("~/api/admin/payouts/failed")]
    public async Task<IActionResult> GetFailedPayouts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetFailedPayoutsAsync(
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Failed payout recovery queue retrieved successfully."));
    }

    [HttpPost("transactions/{providerTransactionRowId:guid}/reconcile")]
    public async Task<IActionResult> ReconcileTransaction(
        Guid providerTransactionRowId,
        [FromBody] ProviderAdminActionRequestDto request,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();

        var result = await _reconciliationService.ReconcileOneAsync(
            providerTransactionRowId,
            ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "PROVIDER_TRANSACTION_RECONCILED",
                Category: "ProviderOperations",
                EntityName: "ProviderTransaction",
                EntityId: providerTransactionRowId.ToString(),
                NewValues: new
                {
                    result.Updated,
                    result.ProviderStatus,
                    result.ReconciledAt
                },
                Metadata: new
                {
                    Reason = reason,
                    Provider = _provider.ProviderName
                },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider transaction reconciled successfully."));
    }

    [HttpPost("transactions/{providerTransactionRowId:guid}/webhook-replay")]
    public async Task<IActionResult> ReplayWebhook(
        Guid providerTransactionRowId,
        [FromBody] ProviderAdminActionRequestDto request,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();

        var transaction = await _db.ProviderTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == providerTransactionRowId &&
                !x.IsDeleted,
                ct);

        if (transaction is null)
        {
            throw new InvalidOperationException("Provider transaction not found.");
        }

        if (!transaction.ProviderStatus.Equals(
                "SUCCESSFUL",
                StringComparison.OrdinalIgnoreCase) &&
            !transaction.ProviderStatus.Equals(
                "COMPLETED",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Webhook replay is available only for successful provider transactions.");
        }

        var providerTransactionDate =
            transaction.ProviderCreatedAt ?? transaction.CreatedAt;

        if (providerTransactionDate < DateTime.UtcNow.AddDays(-90))
        {
            throw new InvalidOperationException(
                "The provider webhook replay retention window has expired for this transaction.");
        }

        var replay = await _provider.ReplayWebhookAsync(
            transaction.ProviderTransactionId,
            ct);

        var result = new ProviderWebhookReplayDto(
            replay.ProviderTransactionId,
            replay.Message,
            replay.ProviderRequestLogId);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "PROVIDER_WEBHOOK_REPLAY_REQUESTED",
                Category: "ProviderOperations",
                EntityName: "ProviderTransaction",
                EntityId: providerTransactionRowId.ToString(),
                NewValues: new
                {
                    transaction.ProviderTransactionId,
                    replay.ProviderRequestLogId
                },
                Metadata: new
                {
                    Reason = reason,
                    Provider = _provider.ProviderName
                },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider webhook replay requested successfully."));
    }

    [HttpPost("~/api/admin/payouts/{payoutId:guid}/retry")]
    public async Task<IActionResult> RetryPayout(
        Guid payoutId,
        [FromBody] ProviderAdminActionRequestDto request,
        CancellationToken ct)
    {
        var reason = NormalizeReason(request.Reason);
        var userId = GetUserId();

        var result = await _payoutService.RetryFailedAsync(
            payoutId,
            userId,
            ct);

        await _audit.RecordAsync(
            new AuditRecordRequest(
                Action: "FAILED_PAYOUT_RETRY_APPROVED",
                Category: "ProviderOperations",
                EntityName: "Payout",
                EntityId: payoutId.ToString(),
                NewValues: result,
                Metadata: new
                {
                    Reason = reason,
                    Provider = _provider.ProviderName
                },
                UserId: userId),
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Failed payout retry completed."));
    }

    [HttpGet("embedded/transfers")]
    public async Task<IActionResult> GetEmbeddedTransfers(
        [FromQuery] string? reference,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.Transfers
            .AsNoTracking()
            .Where(x =>
                x.BusinessCustomerId != null &&
                !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(reference))
        {
            var value = reference.Trim();

            query = query.Where(x =>
                x.Reference == value ||
                x.ExternalReference == value);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Reference,
                x.ExternalReference,
                x.BusinessProfileId,
                x.BusinessCustomerId,
                x.SourceFinancialAccountId,
                x.Status,
                x.ProviderCode,
                x.ProviderTransferId,
                x.ProviderReference,
                x.SourceAmount,
                x.SourceCurrencyCode,
                x.DestinationAmount,
                x.DestinationCurrencyCode,
                x.FailureReason,
                x.CreatedAt,
                x.LastUpdatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            rows,
            "Embedded transfers retrieved successfully."));
    }

    [HttpGet("embedded/payouts")]
    public async Task<IActionResult> GetEmbeddedPayouts(
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var rows = await _db.Payouts
            .AsNoTracking()
            .Where(x =>
                x.ContextEntityType ==
                    nameof(KorridorX.Models.EmbeddedFinance.BusinessCustomer) &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Reference,
                x.ContextEntityId,
                x.FinancialAccountId,
                x.Status,
                x.ProviderCode,
                x.ProviderPayoutId,
                x.ProviderReference,
                x.Amount,
                x.CurrencyCode,
                x.FailureReason,
                x.CreatedAt,
                x.LastUpdatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            rows,
            "Embedded payouts retrieved successfully."));
    }

    [HttpGet("embedded/webhook-deliveries")]
    public async Task<IActionResult> GetEmbeddedWebhookDeliveries(
        [FromQuery] BusinessWebhookDeliveryStatus? status,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.BusinessWebhookDeliveries
            .AsNoTracking()
            .Include(x => x.BusinessWebhookEvent)
            .Include(x => x.BusinessWebhookEndpoint)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.BusinessWebhookEndpointId,
                x.BusinessWebhookEndpoint.BusinessProfileId,
                x.BusinessWebhookEvent.EventId,
                x.BusinessWebhookEvent.EventType,
                x.Status,
                x.AttemptCount,
                x.NextAttemptAt,
                x.LastAttemptAt,
                x.DeliveredAt,
                x.DeadLetteredAt,
                x.LastResponseStatusCode,
                x.ErrorMessage,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            rows,
            "Embedded webhook deliveries retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "A reason is required for this provider operation.");

        var cleaned = reason.Trim();

        if (cleaned.Length > 1000)
            throw new InvalidOperationException(
                "Reason cannot exceed 1000 characters.");

        return cleaned;
    }

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
}