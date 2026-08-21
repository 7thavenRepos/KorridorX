using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedWebhookOutboxStager : IEmbeddedWebhookOutboxStager
{
    private readonly AppDbContext _db;

    public EmbeddedWebhookOutboxStager(AppDbContext db)
    {
        _db = db;
    }

    public Guid Stage(
        Guid businessProfileId,
        string eventType,
        object payload,
        DateTime? occurredAt = null)
    {
        var normalizedType = NormalizeEventType(eventType);
        var now = occurredAt ?? DateTime.UtcNow;

        var evt = new BusinessWebhookEvent
        {
            BusinessProfileId = businessProfileId,
            EventId = $"evt_{Guid.NewGuid():N}",
            EventType = normalizedType,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAt = now
        };

        var endpoints = _db.BusinessWebhookEndpoints
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.Status == BusinessWebhookEndpointStatus.Active &&
                !x.IsDeleted)
            .ToList();

        foreach (var endpoint in endpoints.Where(x =>
                     Matches(x.EventTypesCsv, normalizedType)))
        {
            evt.Deliveries.Add(new BusinessWebhookDelivery
            {
                BusinessWebhookEndpointId = endpoint.Id,
                Status = BusinessWebhookDeliveryStatus.Pending,
                NextAttemptAt = now
            });
        }

        _db.BusinessWebhookEvents.Add(evt);
        return evt.Id;
    }

    private static string NormalizeEventType(string eventType)
    {
        var normalized = (eventType ?? "").Trim().ToLowerInvariant();

        if (normalized.Length == 0 || normalized.Length > 150)
            throw new InvalidOperationException("Webhook event type is invalid.");

        return normalized;
    }

    private static bool Matches(string csv, string eventType)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return false;

        var values = csv.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        return values.Any(x =>
            x == "*" ||
            x.Equals(eventType, StringComparison.OrdinalIgnoreCase));
    }
}
