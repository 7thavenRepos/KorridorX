using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class BusinessWebhookDelivery : BaseEntity
{
    public Guid BusinessWebhookEventId { get; set; }
    public BusinessWebhookEvent BusinessWebhookEvent { get; set; } = null!;
    public Guid BusinessWebhookEndpointId { get; set; }
    public BusinessWebhookEndpoint BusinessWebhookEndpoint { get; set; } = null!;
    public BusinessWebhookDeliveryStatus Status { get; set; } = BusinessWebhookDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid? LockId { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public int? LastResponseStatusCode { get; set; }
    public string? LastResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
}
