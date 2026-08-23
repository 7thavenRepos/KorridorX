using KorridorX.Data;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedWebhookPublisher : IEmbeddedWebhookPublisher
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedWebhookOutboxStager _stager;

    public EmbeddedWebhookPublisher(
        AppDbContext db,
        IEmbeddedWebhookOutboxStager stager)
    {
        _db = db;
        _stager = stager;
    }

    public async Task<Guid> PublishAsync(
        Guid businessProfileId,
        string eventType,
        object payload,
        CancellationToken ct = default)
    {
        var eventId = _stager.Stage(
            businessProfileId,
            eventType,
            payload);

        await _db.SaveChangesAsync(ct);
        return eventId;
    }
}
