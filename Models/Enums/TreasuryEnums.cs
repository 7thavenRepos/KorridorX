namespace KorridorX.Models.Enums;

public enum LiquidityPositionStatus
{
    Unknown = 0,
    Critical = 1,
    Low = 2,
    Healthy = 3,
    AboveTarget = 4
}

public enum SettlementBatchStatus
{
    Draft = 1,
    PendingReconciliation = 2,
    Reconciled = 3,
    Variance = 4,
    Closed = 5
}

public enum SettlementDirection
{
    Inflow = 1,
    Outflow = 2
}

public enum TreasurySwapAmountType
{
    From = 1,
    To = 2
}

public enum TreasuryRebalanceStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Executing = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7
}

public enum SettlementStatementImportStatus
{
    Imported = 1,
    Reconciled = 2,
    Variance = 3,
    Rejected = 4
}

public enum TreasuryLiquidityScopeType
{
    ProviderWallet = 1,
    FinancialAccount = 2
}

public enum TreasuryLiquidityActionType
{
    InternalTransfer = 1,
    ProviderTopUp = 2,
    ProviderSweep = 3,
    ProviderSwap = 4,
    Monitor = 5
}

public enum TreasuryLiquidityAlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

