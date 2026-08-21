using KorridorX.Models.Common;

namespace KorridorX.Models.EmbeddedFinance;

public class BusinessWebhookEvent : BaseEntity
{
    public Guid BusinessProfileId { get; set; }
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public ICollection<BusinessWebhookDelivery> Deliveries { get; set; } = new List<BusinessWebhookDelivery>();
}
