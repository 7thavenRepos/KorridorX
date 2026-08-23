namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedWebhookSender
{
    Task<EmbeddedWebhookSendResult> SendAsync(Guid deliveryId, CancellationToken ct = default);
}

public sealed record EmbeddedWebhookSendResult(bool Success, int? StatusCode, string? ResponseBody, string? ErrorMessage);
