using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Marketplace;

public class BusinessTradingRfq : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid MarketplacePairId { get; set; }
    public MarketplacePair MarketplacePair { get; set; } = null!;
    public FinancialAccountOwnerType RequesterOwnerType { get; set; }
    public Guid RequesterOwnerId { get; set; }
    public FinancialAccountOwnerType CounterpartyOwnerType { get; set; }
    public Guid CounterpartyOwnerId { get; set; }
    public TradeOrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public BusinessTradingRfqStatus Status { get; set; } = BusinessTradingRfqStatus.Open;
    public DateTime ExpiresAt { get; set; }
    public Guid? AcceptedQuoteId { get; set; }
    public Guid? TradeMatchId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public ICollection<BusinessTradingRfqQuote> Quotes { get; set; } = new List<BusinessTradingRfqQuote>();
}
