namespace KorridorX.Services.Webhooks;

public interface IBlaaizWebhookService
{
    Task<BlaaizWebhookResult> ProcessCollectionWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        CancellationToken ct = default);

    Task<BlaaizWebhookResult> ProcessPayoutWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        CancellationToken ct = default);
}

public record BlaaizWebhookResult(
    Guid WebhookEventId,
    string EventType,
    string ProcessingStatus,
    bool IsDuplicate);
