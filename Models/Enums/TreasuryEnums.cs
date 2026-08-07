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
