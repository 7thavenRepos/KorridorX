using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Treasury;

public sealed class ProviderWalletDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderWalletId { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public decimal? MinimumBalance { get; set; }
    public decimal? TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public LiquidityPositionStatus LiquidityStatus { get; set; }
}

public sealed class UpsertLiquidityThresholdRequestDto
{
    [Required, MaxLength(50)] public string ProviderCode { get; set; } = "Blaaiz";
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "";
    [Range(0, double.MaxValue)] public decimal MinimumBalance { get; set; }
    [Range(0, double.MaxValue)] public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LiquidityThresholdDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public decimal MinimumBalance { get; set; }
    public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateSettlementBatchRequestDto
{
    [Required, MaxLength(50)] public string ProviderCode { get; set; } = "Blaaiz";
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
}

public sealed class ReconcileSettlementBatchRequestDto
{
    public decimal ActualNetAmount { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class SettlementBatchItemDto
{
    public Guid Id { get; set; }
    public Guid ProviderTransactionRowId { get; set; }
    public string ProviderTransactionId { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public SettlementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
}

public sealed class SettlementBatchDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public SettlementBatchStatus Status { get; set; }
    public decimal GrossInflows { get; set; }
    public decimal GrossOutflows { get; set; }
    public decimal ExpectedNetAmount { get; set; }
    public decimal? ActualNetAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? ReconciliationNote { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SettlementBatchItemDto> Items { get; set; } = new();
}

public sealed class TreasuryDashboardDto
{
    public DateTime GeneratedAt { get; set; }
    public int ProviderWalletCount { get; set; }
    public int LowLiquidityWalletCount { get; set; }
    public int StaleProviderWalletCount { get; set; }
    public int ActiveSettlementBatches { get; set; }
    public int SettlementVarianceCount { get; set; }
    public List<ProviderWalletDto> Wallets { get; set; } = new();
}

public sealed class UpsertFxMarkupRuleRequestDto
{
    [Required, MaxLength(10)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(10)] public string DestinationCurrencyCode { get; set; } = "";
    [Range(0, 100)] public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateManagedExchangeRateRequestDto
{
    [Required, MaxLength(10)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(10)] public string DestinationCurrencyCode { get; set; } = "";
    [Range(0.00000001, double.MaxValue)] public decimal ProviderRate { get; set; }
    [MaxLength(50)] public string ProviderCode { get; set; } = "Blaaiz";
    [MaxLength(150)] public string? ProviderRateId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class FxMarkupRuleDto
{
    public Guid Id { get; set; }
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ManagedExchangeRateDto
{
    public Guid Id { get; set; }
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }
    public decimal MarkupRate { get; set; }
    public string ProviderCode { get; set; } = "";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class FxOperationsDashboardDto
{
    public DateTime GeneratedAt { get; set; }
    public int ActiveRateCount { get; set; }
    public int StaleRateCount { get; set; }
    public int ActiveMarkupRuleCount { get; set; }
    public List<ManagedExchangeRateDto> Rates { get; set; } = new();
}
