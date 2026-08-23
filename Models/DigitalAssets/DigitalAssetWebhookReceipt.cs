using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetWebhookReceipt : AuditableEntity
{
    public string ProviderCode { get; set; } = "";
    public string ProviderEventId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public DigitalAssetWebhookReceiptStatus Status { get; set; } =
        DigitalAssetWebhookReceiptStatus.Received;
    public string? EventType { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
