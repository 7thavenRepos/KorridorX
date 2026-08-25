using System.Security.Claims;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Compliance,InternalAudit,Operations,Admin,SuperAdmin")]
[ApiController]
[Route("api/admin/embedded-finance")]
public sealed class AdminEmbeddedFinanceController : ControllerBase
{
    private readonly IEmbeddedFinanceAdminQueryService _service;
    private readonly IEmbeddedFinanceAdminCommandService _commands;
    private readonly IEmbeddedFinanceAdminAssuranceService _assurance;

    public AdminEmbeddedFinanceController(
        IEmbeddedFinanceAdminQueryService service,
        IEmbeddedFinanceAdminCommandService commands,
        IEmbeddedFinanceAdminAssuranceService assurance)
    {
        _service = service;
        _commands = commands;
        _assurance = assurance;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetOverviewAsync(ct),
            "Embedded Finance overview retrieved successfully."));


    [HttpGet("assurance")]
    public async Task<IActionResult> Assurance(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct = default)
    {
        var result = await _assurance.GetAsync(
            businessProfileId,
            fromUtc,
            toUtc,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Embedded Finance assurance context retrieved successfully."));
    }

    [HttpGet("assurance.csv")]
    public async Task<IActionResult> ExportAssurance(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct = default)
    {
        var content = await _assurance.ExportCsvAsync(
            businessProfileId,
            fromUtc,
            toUtc,
            ct);

        return File(
            content,
            "text/csv",
            $"embedded-finance-assurance-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

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


    [HttpGet("accounts/{collectionAccountId:guid}/provider-mappings")]
    public async Task<IActionResult> ProviderMappings(
        Guid collectionAccountId,
        CancellationToken ct)
    {
        var result = await _service.GetProviderMappingsAsync(
            collectionAccountId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider account mappings retrieved successfully."));
    }

    [HttpGet("exceptions")]
    public async Task<IActionResult> Exceptions(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] string? sourceType,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetExceptionsAsync(
            businessProfileId,
            sourceType,
            search,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance exception queue retrieved successfully."));
    }

    [HttpGet("activity.csv")]
    public async Task<IActionResult> ExportActivity(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] Guid? businessCustomerId,
        [FromQuery] Guid? collectionAccountId,
        [FromQuery] string? activityType,
        [FromQuery] string? assetCode,
        [FromQuery] string? providerCode,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct = default)
    {
        var content = await _service.ExportActivityCsvAsync(
            businessProfileId,
            businessCustomerId,
            collectionAccountId,
            activityType,
            assetCode,
            providerCode,
            search,
            fromUtc,
            toUtc,
            ct);

        return File(
            content,
            "text/csv",
            $"embedded-finance-activity-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("activity")]
    public async Task<IActionResult> Activity(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] Guid? businessCustomerId,
        [FromQuery] Guid? collectionAccountId,
        [FromQuery] string? activityType,
        [FromQuery] string? assetCode,
        [FromQuery] string? providerCode,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetActivityAsync(
            businessProfileId,
            businessCustomerId,
            collectionAccountId,
            activityType,
            assetCode,
            providerCode,
            search,
            fromUtc,
            toUtc,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded Finance activity retrieved successfully."));
    }


    [HttpGet("activity/{activityType}/{activityId:guid}")]
    public async Task<IActionResult> ActivityDetail(
        string activityType,
        Guid activityId,
        CancellationToken ct)
    {
        var result = await _service.GetActivityDetailAsync(
            activityType,
            activityId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Embedded Finance activity detail retrieved successfully."));
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

    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("applications/{apiApplicationId:guid}/status")]
    public async Task<IActionResult> SetApplicationStatus(
        Guid apiApplicationId,
        [FromBody] EmbeddedFinanceAdminApplicationStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.SetApplicationStatusAsync(
            GetUserId(),
            apiApplicationId,
            request.Status,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "API application status updated successfully."));
    }

    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("applications/{apiApplicationId:guid}/credentials/{credentialId:guid}/revoke")]
    public async Task<IActionResult> RevokeCredential(
        Guid apiApplicationId,
        Guid credentialId,
        [FromBody] EmbeddedFinanceAdminReasonRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.RevokeCredentialAsync(
            GetUserId(),
            apiApplicationId,
            credentialId,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "API credential revoked successfully."));
    }


    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("customers/{businessCustomerId:guid}/status")]
    public async Task<IActionResult> SetCustomerStatus(
        Guid businessCustomerId,
        [FromBody] EmbeddedFinanceAdminCustomerStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.SetCustomerStatusAsync(
            GetUserId(),
            businessCustomerId,
            request.Status,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Business customer status updated successfully."));
    }

    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("accounts/{collectionAccountId:guid}/status")]
    public async Task<IActionResult> SetCollectionAccountStatus(
        Guid collectionAccountId,
        [FromBody] EmbeddedFinanceAdminCollectionAccountStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.SetCollectionAccountStatusAsync(
            GetUserId(),
            collectionAccountId,
            request.Status,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Collection account status updated successfully."));
    }


    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("accounts/{collectionAccountId:guid}/provider-mappings/{providerMappingId:guid}/retry")]
    public async Task<IActionResult> RetryProviderMapping(
        Guid collectionAccountId,
        Guid providerMappingId,
        [FromBody] EmbeddedFinanceAdminReasonRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.RetryProviderMappingAsync(
            GetUserId(),
            collectionAccountId,
            providerMappingId,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider provisioning retry completed successfully."));
    }

    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("webhook-endpoints/{endpointId:guid}/status")]
    public async Task<IActionResult> SetWebhookEndpointStatus(
        Guid endpointId,
        [FromBody] EmbeddedFinanceAdminWebhookStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.SetWebhookEndpointStatusAsync(
            GetUserId(),
            endpointId,
            request.Enabled,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Webhook endpoint status updated successfully."));
    }

    [Authorize(Roles = "Operations,Admin,SuperAdmin")]
    [HttpPost("webhook-deliveries/{deliveryId:guid}/retry")]
    public async Task<IActionResult> RetryWebhookDelivery(
        Guid deliveryId,
        [FromBody] EmbeddedFinanceAdminReasonRequestDto request,
        CancellationToken ct)
    {
        var result = await _commands.RetryWebhookDeliveryAsync(
            GetUserId(),
            deliveryId,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Webhook delivery queued for retry."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException(
                "Invalid authenticated user.");
    }
}
