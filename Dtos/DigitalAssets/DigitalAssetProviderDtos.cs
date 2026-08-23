using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.DigitalAssets;

public sealed class UpsertDigitalAssetProviderConfigurationRequestDto
{
    [Required, MaxLength(50)]
    public string ProviderCode { get; set; } = "";

    [Required, MaxLength(120)]
    public string DisplayName { get; set; } = "";

    [MaxLength(1000)]
    public string? BaseUrl { get; set; }

    public string? WebhookSecret { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsActive { get; set; } = true;
    public bool WebhooksEnabled { get; set; } = true;
    public bool BalanceSyncEnabled { get; set; } = true;
}

public sealed class DigitalAssetProviderConfigurationDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string? WebhookSecretLastFour { get; set; }
    public bool IsActive { get; set; }
    public bool WebhooksEnabled { get; set; }
    public bool BalanceSyncEnabled { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public bool? LastHealthCheckSucceeded { get; set; }
    public string? LastHealthCheckMessage { get; set; }
}

public sealed class DigitalAssetProviderSyncResultDto
{
    public string ProviderCode { get; set; } = "";
    public int BalanceCount { get; set; }
    public DateTime SyncedAt { get; set; }
}

public sealed class DigitalAssetProviderHealthDto
{
    public string ProviderCode { get; set; } = "";
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = "";
    public DateTime CheckedAt { get; set; }
}

public sealed class DigitalAssetFeeExceptionDto
{
    public Guid WithdrawalId { get; set; }
    public string AssetCode { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public decimal QuotedNetworkFee { get; set; }
    public decimal ActualNetworkFee { get; set; }
    public decimal Variance { get; set; }
    public DateTime CreatedAt { get; set; }
}
