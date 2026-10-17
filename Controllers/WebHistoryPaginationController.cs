using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Pagination;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class WebHistoryPaginationController(WebHistoryPaginationService service) : ControllerBase
{
    [HttpGet("business/digital-assets/activity/paged")]
    public async Task<IActionResult> DigitalActivity([FromQuery] WebHistoryPageQuery query, CancellationToken ct)
    {
        var result = await service.GetDigitalActivityAsync(GetUserId(), query, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business digital-asset activity retrieved successfully."));
    }

    [HttpGet("business/embedded/webhook-deliveries/paged")]
    public async Task<IActionResult> WebhookDeliveries([FromQuery] WebHistoryPageQuery query,
        [FromQuery, EnumDataType(typeof(BusinessWebhookDeliveryStatus))] BusinessWebhookDeliveryStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await service.GetWebhookDeliveriesAsync(GetUserId(), query, status, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Webhook deliveries retrieved successfully."));
    }

    [HttpGet("admin/digital-asset-providers/reconciliation-exceptions/paged")]
    [Authorize(Roles = "Admin,SuperAdmin,Compliance,Operations")]
    public async Task<IActionResult> ReconciliationExceptions([FromQuery] WebHistoryPageQuery query, CancellationToken ct)
    {
        var result = await service.GetReconciliationExceptionsAsync(query, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Digital-asset reconciliation exceptions retrieved successfully."));
    }

    [HttpGet("admin/digital-asset-providers/fee-exceptions/paged")]
    [Authorize(Roles = "Admin,SuperAdmin,Compliance,Operations")]
    public async Task<IActionResult> FeeExceptions([FromQuery] WebHistoryPageQuery query,
        [FromQuery, Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal minimumAbsoluteVariance = 0.00000001m,
        CancellationToken ct = default)
    {
        var result = await service.GetFeeExceptionsAsync(query, minimumAbsoluteVariance, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Digital-asset network-fee exceptions retrieved successfully."));
    }

    [HttpGet("business/digital-assets/deposit-intents/paged")]
    public async Task<IActionResult> DepositIntents([FromQuery] WebHistoryPageQuery query, CancellationToken ct)
    {
        var result = await service.GetDepositIntentsAsync(GetUserId(), query, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business deposit intents retrieved successfully."));
    }

    [HttpGet("business/digital-assets/withdrawals/paged")]
    public async Task<IActionResult> Withdrawals([FromQuery] WebHistoryPageQuery query, CancellationToken ct)
    {
        var result = await service.GetWithdrawalsAsync(GetUserId(), query, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business withdrawals retrieved successfully."));
    }

    [HttpGet("admin/instant/settlement-exceptions/paged")]
    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    public async Task<IActionResult> SettlementExceptions([FromQuery] WebHistoryPageQuery query, CancellationToken ct)
    {
        var result = await service.GetSettlementExceptionsAsync(query, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Instant settlement exceptions retrieved successfully."));
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
