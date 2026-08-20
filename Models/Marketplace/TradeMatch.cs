using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Marketplace;

public class TradeMatch : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid MarketplacePairId { get; set; }
    public MarketplacePair MarketplacePair { get; set; } = null!;

    public Guid BuyOrderId { get; set; }
    public TradeOrder BuyOrder { get; set; } = null!;
    public Guid SellOrderId { get; set; }
    public TradeOrder SellOrder { get; set; } = null!;
    public Guid MakerOrderId { get; set; }
    public Guid TakerOrderId { get; set; }

    public decimal Price { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal QuoteQuantity { get; set; }
    public TradeMatchStatus Status { get; set; } = TradeMatchStatus.Matched;

    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettlementStartedAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }

    public Trade? Trade { get; set; }
}
