namespace KorridorX.Models.Enums;

public enum FinancialAccountOwnerType
{
    User = 1,
    Business = 2,
    BusinessCustomer = 3,
    Platform = 10,
    Treasury = 11,
    Provider = 12
}

public enum FinancialAccountType
{
    Customer = 1,
    House = 10,
    Treasury = 11,
    ProviderClearing = 20,
    Settlement = 21,
    Revenue = 22,
    Suspense = 30
}

public enum FinancialAccountStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum LedgerBalanceBucket
{
    External = 1,
    Available = 2,
    Held = 3,
    Settlement = 4
}

public enum LedgerPostingSide
{
    Debit = 1,
    Credit = 2
}

public enum LedgerTransactionType
{
    FundingCredit = 1,
    ExternalCollectionCredit = 2,
    TransferReservation = 3,
    ReservationCapture = 4,
    ReservationRelease = 5,
    ManualAdjustment = 6,
    Reversal = 7,
    MarketplaceReservation = 8,
    InstantReservation = 9,
    Deposit = 10,
    Withdrawal = 11,
    Trade = 20,
    Conversion = 21,
    Fee = 30,
    Refund = 31,
    Treasury = 40,
    Settlement = 41
}

public enum LedgerTransactionStatus
{
    Posted = 1,
    Reversed = 2
}

public enum FinancialReservationType
{
    Transfer = 1,
    MarketplaceTrade = 2,
    InstantTrade = 3,
    Withdrawal = 4,
    Other = 99
}

public enum FinancialReservationStatus
{
    Active = 1,
    Captured = 2,
    Released = 3
}
