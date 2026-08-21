using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class BusinessWebhookEndpoint : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid ApiApplicationId { get; set; }
    public ApiApplication ApiApplication { get; set; } = null!;
    public string Url { get; set; } = "";
    public string EventTypesCsv { get; set; } = "";
    public string SigningSecretProtected { get; set; } = "";
    public string SigningSecretLastFour { get; set; } = "";
    public BusinessWebhookEndpointStatus Status { get; set; } = BusinessWebhookEndpointStatus.Active;
    public int MaxAttempts { get; set; } = 8;
    public ICollection<BusinessWebhookDelivery> Deliveries { get; set; } = new List<BusinessWebhookDelivery>();
}
