namespace KorridorX.Models.Enums;

public enum InstantPairStatus
{
    Active = 1,
    Paused = 2,
    Disabled = 3
}

public enum InstantQuoteStatus
{
    Active = 1,
    Consumed = 2,
    Expired = 3,
    Cancelled = 4
}

public enum InstantTradeStatus
{
    PendingSettlement = 1,
    Settling = 2,
    Completed = 3,
    Failed = 4,
    Reversed = 5
}
