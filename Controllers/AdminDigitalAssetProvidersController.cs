using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.DigitalAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/admin/digital-asset-providers")]
[Authorize(Roles = "Admin,SuperAdmin,Compliance,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminDigitalAssetProvidersController : ControllerBase
{
    private readonly IDigitalAssetProviderOperationsService _service;

    public AdminDigitalAssetProvidersController(
        IDigitalAssetProviderOperationsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfigurations(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetConfigurationsAsync(ct),
            "Digital-asset provider configurations retrieved successfully."));

    [HttpPut]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertDigitalAssetProviderConfigurationRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.UpsertConfigurationAsync(
                GetUserId(),
                request,
                ct),
            "Digital-asset provider configuration saved successfully."));

    [HttpPost("{providerCode}/health")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> Health(
        string providerCode,
        [FromBody] DigitalAssetProviderAdminActionRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.CheckHealthAsync(
                providerCode,
                GetUserId(),
                request.Reason,
                ct),
            "Digital-asset provider health check completed."));

    [HttpPost("{providerCode}/balances/sync")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> SyncBalances(
        string providerCode,
        [FromBody] DigitalAssetProviderAdminActionRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.SyncBalancesAsync(
                providerCode,
                GetUserId(),
                request.Reason,
                ct),
            "Digital-asset provider balances synchronized successfully."));

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] string? providerCode = null,
        [FromQuery] string? assetCode = null,
        [FromQuery] string? networkCode = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetBalancesAsync(
            providerCode,
            assetCode,
            networkCode,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Digital-asset provider balances retrieved successfully."));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? providerCode = null,
        [FromQuery] string? assetCode = null,
        [FromQuery] string? networkCode = null,
        [FromQuery] DigitalAssetTransactionDirection? direction = null,
        [FromQuery] DigitalAssetTransactionStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetNetworkTransactionsAsync(
            providerCode,
            assetCode,
            networkCode,
            direction,
            status,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Digital-asset network transactions retrieved successfully."));
    }

    [HttpGet("transactions/{transactionId:guid}")]
    public async Task<IActionResult> GetTransaction(
        Guid transactionId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetNetworkTransactionAsync(
                transactionId,
                ct),
            "Digital-asset network transaction retrieved successfully."));

    [HttpGet("webhooks")]
    public async Task<IActionResult> GetWebhooks(
        [FromQuery] string? providerCode = null,
        [FromQuery] DigitalAssetWebhookReceiptStatus? status = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetWebhookReceiptsAsync(
            providerCode,
            status,
            eventType,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Digital-asset webhook receipts retrieved successfully."));
    }

    [HttpGet("webhooks/{webhookReceiptId:guid}")]
    public async Task<IActionResult> GetWebhook(
        Guid webhookReceiptId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetWebhookReceiptAsync(
                webhookReceiptId,
                ct),
            "Digital-asset webhook receipt retrieved successfully."));

    [HttpGet("reconciliation-exceptions")]
    public async Task<IActionResult> ReconciliationExceptions(
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetReconciliationExceptionsAsync(
                take,
                ct),
            "Digital-asset reconciliation exceptions retrieved successfully."));

    [HttpGet("fee-exceptions")]
    public async Task<IActionResult> FeeExceptions(
        [FromQuery] decimal minimumAbsoluteVariance = 0.00000001m,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.GetFeeExceptionsAsync(
                minimumAbsoluteVariance,
                take,
                ct),
            "Digital-asset network-fee exceptions retrieved successfully."));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException(
                "Invalid authenticated user.");

        return userId;
    }
}