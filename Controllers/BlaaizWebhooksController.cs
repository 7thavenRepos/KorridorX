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
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["x-blaaiz-signature"].FirstOrDefault();
        var timestamp = Request.Headers["x-blaaiz-timestamp"].FirstOrDefault();

        var result = await _webhookService.ProcessCollectionWebhookAsync(
            rawPayload,
            signature,
            timestamp,
            ct);

        return Ok(ApiResponses.Ok(result, "Blaaiz webhook accepted."));
    }
}
