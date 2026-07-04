using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Webhooks;

public class WebhookProcessingAttempt : BaseEntity
{
    public Guid WebhookEventId { get; set; }
    public WebhookEvent WebhookEvent { get; set; } = null!;

    public WebhookProcessingStatus Status { get; set; } = WebhookProcessingStatus.Processing;

    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}