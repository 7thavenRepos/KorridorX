using System.Security.Claims;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;
[Authorize, ApiController, Route("api/business/embedded")]
public sealed class BusinessEmbeddedFinanceController : ControllerBase
{
    private readonly IEmbeddedFinanceManagementService _service;
    private readonly IEmbeddedWebhookManagementService _webhooks;
    public BusinessEmbeddedFinanceController(
        IEmbeddedFinanceManagementService service,
        IEmbeddedWebhookManagementService webhooks)
    {
        _service = service;
        _webhooks = webhooks;
    }
    [HttpGet("applications")] public async Task<IActionResult> Applications(CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetApplicationsAsync(GetUserId(),ct),"API applications retrieved successfully."));
    [HttpPost("applications")] public async Task<IActionResult> CreateApplication([FromBody] CreateApiApplicationRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateApplicationAsync(GetUserId(),request,ct),"API application created successfully."));
    [HttpGet("applications/{apiApplicationId:guid}/credentials")] public async Task<IActionResult> Credentials(Guid apiApplicationId,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetCredentialsAsync(GetUserId(),apiApplicationId,ct),"API credentials retrieved successfully."));
    [HttpPost("applications/{apiApplicationId:guid}/credentials")] public async Task<IActionResult> CreateCredential(Guid apiApplicationId,[FromBody] CreateApiCredentialRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateCredentialAsync(GetUserId(),apiApplicationId,request,ct),"API credential created. Store the API key now because it cannot be retrieved again."));
    [HttpPost("applications/{apiApplicationId:guid}/credentials/{credentialId:guid}/revoke")] public async Task<IActionResult> RevokeCredential(Guid apiApplicationId,Guid credentialId,CancellationToken ct){await _service.RevokeCredentialAsync(GetUserId(),apiApplicationId,credentialId,ct);return Ok(ApiResponses.Ok(new{apiApplicationId,credentialId},"API credential revoked successfully."));}
    [HttpGet("webhooks")]
    public async Task<IActionResult> Webhooks(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _webhooks.GetEndpointsAsync(GetUserId(), ct), "Webhook endpoints retrieved successfully."));

    [HttpPost("webhooks")]
    public async Task<IActionResult> CreateWebhook([FromBody] CreateBusinessWebhookEndpointRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _webhooks.CreateEndpointAsync(GetUserId(), request, ct),
            "Webhook endpoint created. Store the signing secret now because it cannot be retrieved again."));

    [HttpPost("webhooks/{endpointId:guid}/rotate-secret")]
    public async Task<IActionResult> RotateWebhookSecret(Guid endpointId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _webhooks.RotateSecretAsync(GetUserId(), endpointId, ct), "Webhook signing secret rotated successfully."));

    [HttpPost("webhooks/{endpointId:guid}/status")]
    public async Task<IActionResult> SetWebhookStatus(Guid endpointId, [FromQuery] bool enabled, CancellationToken ct)
    {
        await _webhooks.SetStatusAsync(GetUserId(), endpointId, enabled, ct);
        return Ok(ApiResponses.Ok(new { endpointId, enabled }, "Webhook endpoint status updated."));
    }
    [HttpGet("webhook-deliveries")]
    public async Task<IActionResult> WebhookDeliveries([FromQuery] BusinessWebhookDeliveryStatus? status, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(await _webhooks.GetDeliveriesAsync(GetUserId(), status, take, ct), "Webhook deliveries retrieved successfully."));

    [HttpPost("webhook-deliveries/{deliveryId:guid}/retry")]
    public async Task<IActionResult> RetryWebhookDelivery(Guid deliveryId, CancellationToken ct)
    {
        await _webhooks.RetryDeliveryAsync(GetUserId(), deliveryId, ct);
        return Ok(ApiResponses.Ok(new { deliveryId }, "Webhook delivery queued for retry."));
    }

    private Guid GetUserId(){var v=User.FindFirstValue(ClaimTypes.NameIdentifier);return Guid.TryParse(v,out var id)?id:throw new UnauthorizedAccessException("Invalid authenticated user.");}
}
