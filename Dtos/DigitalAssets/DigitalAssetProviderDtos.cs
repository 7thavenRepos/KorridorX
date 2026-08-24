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

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class DigitalAssetProviderAdminActionRequestDto
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
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

public sealed class DigitalAssetProviderBalanceDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderWalletId { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public string? NetworkCode { get; set; }
    public Guid? AssetNetworkId { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public sealed class DigitalAssetNetworkTransactionDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderTransactionId { get; set; } = "";
    public string? ProviderReference { get; set; }
    public Guid AssetNetworkId { get; set; }
    public string AssetCode { get; set; } = "";
    public string NetworkCode { get; set; } = "";
    public DigitalAssetTransactionDirection Direction { get; set; }
    public DigitalAssetTransactionStatus Status { get; set; }
    public string? TransactionHash { get; set; }
    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }
    public string? DestinationTag { get; set; }
    public decimal Amount { get; set; }
    public decimal NetworkFee { get; set; }
    public int Confirmations { get; set; }
    public int RequiredConfirmations { get; set; }
    public long? BlockNumber { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? PayoutId { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public sealed class DigitalAssetWebhookReceiptDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderEventId { get; set; } = "";
    public DigitalAssetWebhookReceiptStatus Status { get; set; }
    public string? EventType { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
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