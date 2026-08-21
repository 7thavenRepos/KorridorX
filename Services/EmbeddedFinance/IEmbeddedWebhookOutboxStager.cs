namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedWebhookOutboxStager
{
    Guid Stage(
        Guid businessProfileId,
        string eventType,
        object payload,
        DateTime? occurredAt = null);
}
