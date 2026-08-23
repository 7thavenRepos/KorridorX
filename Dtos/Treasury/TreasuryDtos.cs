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
    public TreasuryLiquidityScopeType ScopeType { get; set; } =
        TreasuryLiquidityScopeType.ProviderWallet;
    [Required, MaxLength(50)] public string ProviderCode { get; set; } = "Blaaiz";
    [Required, MaxLength(20)] public string CurrencyCode { get; set; } = "";
    [MaxLength(50)] public string? NetworkCode { get; set; }
    public FinancialAccountType? FinancialAccountType { get; set; }
    [Range(0, double.MaxValue)] public decimal MinimumBalance { get; set; }
    [Range(0, double.MaxValue)] public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LiquidityThresholdDto
{
    public Guid Id { get; set; }
    public TreasuryLiquidityScopeType ScopeType { get; set; }
    public string ProviderCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string? NetworkCode { get; set; }
    public FinancialAccountType? FinancialAccountType { get; set; }
    public decimal MinimumBalance { get; set; }
    public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateSettlementBatchRequestDto
{
    [Required, MaxLength(50)] public string ProviderCode { get; set; } = "Blaaiz";
    [Required, MaxLength(20)] public string CurrencyCode { get; set; } = "";
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
    [Required, MaxLength(20)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(20)] public string DestinationCurrencyCode { get; set; } = "";
    [Range(0, 100)] public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateManagedExchangeRateRequestDto
{
    [Required, MaxLength(20)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(20)] public string DestinationCurrencyCode { get; set; } = "";
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

public sealed class CreateTreasuryRebalanceRequestDto
{
    public Guid FromProviderWalletBalanceId { get; set; }
    public Guid ToProviderWalletBalanceId { get; set; }
    [Range(0.1, double.MaxValue)] public decimal Amount { get; set; }
    public TreasurySwapAmountType AmountType { get; set; } = TreasurySwapAmountType.From;
    [MaxLength(1000)] public string? Reason { get; set; }
}

public sealed class ReviewTreasuryRebalanceRequestDto
{
    public bool Approve { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}

public sealed class TreasuryRebalanceDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public Guid FromProviderWalletBalanceId { get; set; }
    public Guid ToProviderWalletBalanceId { get; set; }
    public string FromCurrencyCode { get; set; } = "";
    public string ToCurrencyCode { get; set; } = "";
    public decimal RequestedAmount { get; set; }
    public TreasurySwapAmountType AmountType { get; set; }
    public TreasuryRebalanceStatus Status { get; set; }
    public string? Reason { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public string? ProviderSwapId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReference { get; set; }
    public decimal? FromAmount { get; set; }
    public decimal? FromAmountMinusFees { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? CustomExchangeRate { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? FailedAt { get; set; }
}

public sealed class TreasuryRebalanceSuggestionDto
{
    public Guid FromProviderWalletBalanceId { get; set; }
    public Guid ToProviderWalletBalanceId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string FromCurrencyCode { get; set; } = "";
    public string ToCurrencyCode { get; set; } = "";
    public decimal SourceExcessAboveTarget { get; set; }
    public decimal DestinationShortfallToTarget { get; set; }
}

public sealed class ImportSettlementStatementFormDto
{
    [Required] public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class SettlementStatementImportDto
{
    public Guid Id { get; set; }
    public Guid SettlementBatchId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public SettlementStatementImportStatus Status { get; set; }
    public int RowCount { get; set; }
    public int MatchedRowCount { get; set; }
    public int UnmatchedRowCount { get; set; }
    public decimal GrossCredits { get; set; }
    public decimal GrossDebits { get; set; }
    public decimal NetAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? Note { get; set; }
    public DateTime ImportedAt { get; set; }
}

public sealed class TreasuryLiquidityPositionDto
{
    public TreasuryLiquidityScopeType ScopeType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public string? ProviderCode { get; set; }
    public string? NetworkCode { get; set; }
    public FinancialAccountType? FinancialAccountType { get; set; }
    public FinancialAccountStatus? FinancialAccountStatus { get; set; }
    public decimal SettledBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public decimal ExternalBalance { get; set; }
    public decimal PendingNetworkInbound { get; set; }
    public decimal PendingNetworkOutbound { get; set; }
    public decimal? MinimumBalance { get; set; }
    public decimal? TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public LiquidityPositionStatus LiquidityStatus { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsStale { get; set; }
}

public sealed class TreasuryAssetExposureDto
{
    public string AssetCode { get; set; } = "";
    public decimal HouseAvailable { get; set; }
    public decimal TreasuryAvailable { get; set; }
    public decimal ProviderClearingAvailable { get; set; }
    public decimal SettlementAvailable { get; set; }
    public decimal ExternalProviderBalance { get; set; }
    public decimal PendingNetworkInbound { get; set; }
    public decimal PendingNetworkOutbound { get; set; }
    public decimal ObservedLiquidity { get; set; }
}

public sealed class TreasuryLiquidityAlertDto
{
    public TreasuryLiquidityAlertSeverity Severity { get; set; }
    public TreasuryLiquidityScopeType ScopeType { get; set; }
    public Guid SourceId { get; set; }
    public string AssetCode { get; set; } = "";
    public string? ProviderCode { get; set; }
    public string? NetworkCode { get; set; }
    public LiquidityPositionStatus LiquidityStatus { get; set; }
    public string Message { get; set; } = "";
}

public sealed class TreasuryUnifiedRebalanceSuggestionDto
{
    public TreasuryLiquidityActionType ActionType { get; set; }
    public string AssetCode { get; set; } = "";
    public Guid? FromFinancialAccountId { get; set; }
    public Guid? ToFinancialAccountId { get; set; }
    public Guid? ProviderWalletBalanceId { get; set; }
    public string? ProviderCode { get; set; }
    public string? NetworkCode { get; set; }
    public decimal SuggestedAmount { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class ExecuteInternalLiquidityTransferRequestDto
{
    public Guid FromFinancialAccountId { get; set; }
    public Guid ToFinancialAccountId { get; set; }

    [Range(0.00000001, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class InternalLiquidityTransferResultDto
{
    public Guid LedgerTransactionId { get; set; }
    public string Reference { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public Guid FromFinancialAccountId { get; set; }
    public Guid ToFinancialAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal SourceAvailableAfter { get; set; }
    public decimal DestinationAvailableAfter { get; set; }
    public DateTime PostedAt { get; set; }
}

