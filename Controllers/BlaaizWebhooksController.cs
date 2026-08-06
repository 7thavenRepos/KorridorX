using KorridorX.Infrastructure;
using KorridorX.Services.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/webhooks/blaaiz")]
public class BlaaizWebhooksController : ControllerBase
{
    private readonly IBlaaizWebhookService _webhookService;

    public BlaaizWebhooksController(IBlaaizWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost("collection")]
    public async Task<IActionResult> ReceiveCollectionWebhook(CancellationToken ct)
    {
        var result = await _webhookService.ProcessCollectionWebhookAsync(
            await ReadBodyAsync(ct),
            Request.Headers["x-blaaiz-signature"].FirstOrDefault(),
            Request.Headers["x-blaaiz-timestamp"].FirstOrDefault(),
            ct);

        return Ok(ApiResponses.Ok(result, "Blaaiz collection webhook accepted."));
    }

    [HttpPost("payout")]
    public async Task<IActionResult> ReceivePayoutWebhook(CancellationToken ct)
    {
        var result = await _webhookService.ProcessPayoutWebhookAsync(
            await ReadBodyAsync(ct),
            Request.Headers["x-blaaiz-signature"].FirstOrDefault(),
            Request.Headers["x-blaaiz-timestamp"].FirstOrDefault(),
            ct);

        return Ok(ApiResponses.Ok(result, "Blaaiz payout webhook accepted."));
    }

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        return await reader.ReadToEndAsync(ct);
    }
}
