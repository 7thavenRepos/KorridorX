namespace KorridorX.Models.Enums;

public enum BusinessWalletStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum BusinessLedgerAccountType
{
    External = 1,
    Available = 2,
    Held = 3,
    Settlement = 4
}

public enum BusinessLedgerEntrySide
{
    Debit = 1,
    Credit = 2
}

public enum BusinessLedgerTransactionType
{
    FundingCredit = 1,
    ExternalCollectionCredit = 2,
    TransferReservation = 3,
    ReservationCapture = 4,
    ReservationRelease = 5,
    ManualAdjustment = 6,
    Reversal = 7
}

public enum BusinessLedgerTransactionStatus
{
    Posted = 1,
    Reversed = 2
}

public enum BusinessWalletReservationStatus
{
    Active = 1,
    Captured = 2,
    Released = 3
}

public enum BusinessWalletAdjustmentDirection
{
    Credit = 1,
    Debit = 2
}
