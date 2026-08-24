using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Compliance,Operations,Admin,SuperAdmin")]
[ApiController]
[Route("api/admin/embedded-finance")]
public sealed class AdminEmbeddedFinanceController : ControllerBase
{
    private readonly IEmbeddedFinanceAdminQueryService _service;

    public AdminEmbeddedFinanceController(
        IEmbeddedFinanceAdminQueryService service)
    {
        _service = service;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetOverviewAsync(ct),
            "Embedded Finance overview retrieved successfully."));

    [HttpGet("businesses")]
    public async Task<IActionResult> Businesses(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetBusinessesAsync(
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance businesses retrieved successfully."));
    }

    [HttpGet("applications")]
    public async Task<IActionResult> Applications(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] ApiApplicationStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetApplicationsAsync(
            businessProfileId,
            status,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance API applications retrieved successfully."));
    }

    [HttpGet("applications/{apiApplicationId:guid}/credentials")]
    public async Task<IActionResult> Credentials(
        Guid apiApplicationId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetCredentialsAsync(apiApplicationId, ct),
            "Embedded Finance API credential metadata retrieved successfully."));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] BusinessCustomerStatus? status,
        [FromQuery] string? countryCode,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetCustomersAsync(
            businessProfileId,
            status,
            countryCode,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance customers retrieved successfully."));
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] Guid? businessCustomerId,
        [FromQuery] CollectionAccountStatus? status,
        [FromQuery] string? assetCode,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAccountsAsync(
            businessProfileId,
            businessCustomerId,
            status,
            assetCode,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance collection accounts retrieved successfully."));
    }

    [HttpGet("webhook-endpoints")]
    public async Task<IActionResult> WebhookEndpoints(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] BusinessWebhookEndpointStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetWebhookEndpointsAsync(
            businessProfileId,
            status,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance webhook endpoints retrieved successfully."));
    }

    [HttpGet("webhook-deliveries")]
    public async Task<IActionResult> WebhookDeliveries(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] BusinessWebhookDeliveryStatus? status,
        [FromQuery] string? eventType,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetWebhookDeliveriesAsync(
            businessProfileId,
            status,
            eventType,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance webhook deliveries retrieved successfully."));
    }
}
