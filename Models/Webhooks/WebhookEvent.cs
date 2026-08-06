using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Webhooks;

public class WebhookEvent : BaseEntity
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;

    public string? ProviderEventId { get; set; }
    public string EventType { get; set; } = "";

    public string? SignatureHeader { get; set; }
    public string? TimestampHeader { get; set; }
    public string RawPayloadJson { get; set; } = "";

    public WebhookProcessingStatus ProcessingStatus { get; set; } = WebhookProcessingStatus.Pending;

    public bool IsDuplicate { get; set; } = false;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<WebhookProcessingAttempt> Attempts { get; set; } = new List<WebhookProcessingAttempt>();
}
