using KorridorX.Models.Common;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetProviderConfiguration : AuditableEntity
{
    public string ProviderCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string? WebhookSecretProtected { get; set; }
    public string? WebhookSecretLastFour { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsActive { get; set; } = true;
    public bool WebhooksEnabled { get; set; } = true;
    public bool BalanceSyncEnabled { get; set; } = true;
    public DateTime? LastHealthCheckAt { get; set; }
    public bool? LastHealthCheckSucceeded { get; set; }
    public string? LastHealthCheckMessage { get; set; }
}
