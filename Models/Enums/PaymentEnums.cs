namespace KorridorX.Models.Enums;

public enum CollectionStatus
{
    Pending = 1,
    Initiated = 2,
    Processing = 3,
    Successful = 4,
    Failed = 5,
    Cancelled = 6,
    Refunded = 7,
    RefundPending = 8,
    Expired = 9,
    RefundFailed = 10
}

public enum PayoutStatus
{
    Pending = 1,
    Initiated = 2,
    Processing = 3,
    Successful = 4,
    Failed = 5,
    Reversed = 6
}

public enum PaymentMethod
{
    BankTransfer = 1,
    Card = 2,
    Interac = 3,
    Ach = 4,
    Wire = 5,
    Sepa = 6,
    MobileMoney = 7,
    VirtualAccount = 8
}