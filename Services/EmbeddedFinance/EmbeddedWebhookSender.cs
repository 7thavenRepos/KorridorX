using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedWebhookSender : IEmbeddedWebhookSender
{
    public const string HttpClientName = "EmbeddedWebhooks";
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly IEmbeddedWebhookUrlSecurityValidator _urlSecurity;

    public EmbeddedWebhookSender(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IEmbeddedWebhookUrlSecurityValidator urlSecurity)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector("KorridorX.EmbeddedFinance.WebhookSigningSecret.v1");
        _urlSecurity = urlSecurity;
    }

    public async Task<EmbeddedWebhookSendResult> SendAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await _db.BusinessWebhookDeliveries.AsNoTracking()
            .Include(x => x.BusinessWebhookEvent)
            .Include(x => x.BusinessWebhookEndpoint)
            .FirstOrDefaultAsync(x => x.Id == deliveryId, ct);

        if (delivery is null) return new(false, null, null, "Webhook delivery not found.");

        var endpoint = delivery.BusinessWebhookEndpoint;
        if (endpoint.Status != BusinessWebhookEndpointStatus.Active || endpoint.IsDeleted)
            return new(false, null, null, "Webhook endpoint is disabled.");

        string validatedUrl;
        try { validatedUrl = await _urlSecurity.ValidateAsync(endpoint.Url, ct); }
        catch (InvalidOperationException ex)
        {
            return new(false, null, null, $"Webhook endpoint failed security validation: {ex.Message}");
        }

        string secret;
        try { secret = _protector.Unprotect(endpoint.SigningSecretProtected); }
        catch (Exception ex) { return new(false, null, null, $"Webhook signing secret could not be unprotected: {ex.Message}"); }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = delivery.BusinessWebhookEvent.PayloadJson;
        var signed = $"{timestamp}.{payload}";
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed)))
            .ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, validatedUrl);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-KorridorX-Event-Id", delivery.BusinessWebhookEvent.EventId);
        request.Headers.TryAddWithoutValidation("X-KorridorX-Event-Type", delivery.BusinessWebhookEvent.EventType);
        request.Headers.TryAddWithoutValidation("X-KorridorX-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-KorridorX-Signature", $"v1={signature}");

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var truncated = body.Length <= 4000 ? body : body[..4000];
            return response.IsSuccessStatusCode
                ? new(true, (int)response.StatusCode, truncated, null)
                : new(false, (int)response.StatusCode, truncated, $"Webhook endpoint returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, null, null, ex.Message);
        }
    }
}
