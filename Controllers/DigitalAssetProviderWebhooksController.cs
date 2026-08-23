using System.Text;
using KorridorX.Configuration;
using KorridorX.Services.DigitalAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/webhooks/digital-assets/{providerCode}")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class DigitalAssetProviderWebhooksController : ControllerBase
{
    private readonly IDigitalAssetProviderWebhookService _service;

    public DigitalAssetProviderWebhooksController(
        IDigitalAssetProviderWebhookService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        string providerCode,
        CancellationToken ct)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var payload = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var headers = Request.Headers.ToDictionary(
            x => x.Key,
            x => x.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        await _service.ProcessAsync(providerCode, payload, headers, ct);
        return Ok(new { received = true });
    }
}
