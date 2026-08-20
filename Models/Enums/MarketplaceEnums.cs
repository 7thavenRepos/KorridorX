namespace KorridorX.Models.Enums;

public enum MarketplacePairStatus
{
    Active = 1,
    Paused = 2,
    Disabled = 3
}

public enum TradeOrderSide
{
    Buy = 1,
    Sell = 2
}

public enum TradeOrderType
{
    Limit = 1,
    Market = 2
}

public enum TradeOrderTimeInForce
{
    GoodTillCancelled = 1,
    ImmediateOrCancel = 2,
    FillOrKill = 3,
    GoodTillTime = 4
}

public enum TradeOrderStatus
{
    PendingReservation = 1,
    Open = 2,
    PartiallyFilled = 3,
    Filled = 4,
    Cancelled = 5,
    Expired = 6,
    Rejected = 7
}

public enum TradeMatchStatus
{
    Matched = 1,
    SettlementPending = 2,
    Settled = 3,
    Failed = 4,
    Cancelled = 5
}

public enum TradeStatus
{
    PendingSettlement = 1,
    Settling = 2,
    Completed = 3,
    Failed = 4,
    Reversed = 5
}
