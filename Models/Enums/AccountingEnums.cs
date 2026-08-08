namespace KorridorX.Models.Enums;

public enum AccountingAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum AccountingPeriodStatus
{
    Open = 1,
    Closed = 2
}

public enum JournalEntryStatus
{
    Posted = 1,
    Reversed = 2
}

public enum AccountingSourceType
{
    TransferFee = 1,
    FxSpread = 2,
    ProviderFee = 3,
    SettlementVariance = 4,
    RefundRevenueReversal = 5,
    ManualAdjustment = 6,
    JournalReversal = 7,
    ProviderInvoiceAdjustment = 8,
    Accrual = 9,
    AccrualReversal = 10
}
