namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedWebhookPublisher
{
    Task<Guid> PublishAsync(Guid businessProfileId, string eventType, object payload, CancellationToken ct = default);
}
