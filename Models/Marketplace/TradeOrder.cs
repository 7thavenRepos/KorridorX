using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Models.Marketplace;

public class TradeOrder : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid MarketplacePairId { get; set; }
    public MarketplacePair MarketplacePair { get; set; } = null!;

    public FinancialAccountOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    public Guid BaseFinancialAccountId { get; set; }
    public FinancialAccount BaseFinancialAccount { get; set; } = null!;
    public Guid QuoteFinancialAccountId { get; set; }
    public FinancialAccount QuoteFinancialAccount { get; set; } = null!;

    public Guid? ReservationId { get; set; }
    public FinancialReservation? Reservation { get; set; }

    public TradeOrderSide Side { get; set; }
    public TradeOrderType OrderType { get; set; } = TradeOrderType.Limit;
    public TradeOrderTimeInForce TimeInForce { get; set; } = TradeOrderTimeInForce.GoodTillCancelled;
    public TradeOrderStatus Status { get; set; } = TradeOrderStatus.PendingReservation;

    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? AverageFillPrice { get; set; }

    public DateTime? OpenedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<TradeMatch> BuyMatches { get; set; } = new List<TradeMatch>();
    public ICollection<TradeMatch> SellMatches { get; set; } = new List<TradeMatch>();
}
