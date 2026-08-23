using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Models.Marketplace;

public class Trade : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid TradeMatchId { get; set; }
    public TradeMatch TradeMatch { get; set; } = null!;
    public Guid MarketplacePairId { get; set; }
    public MarketplacePair MarketplacePair { get; set; } = null!;

    public Guid BuyerBaseFinancialAccountId { get; set; }
    public FinancialAccount BuyerBaseFinancialAccount { get; set; } = null!;
    public Guid BuyerQuoteFinancialAccountId { get; set; }
    public FinancialAccount BuyerQuoteFinancialAccount { get; set; } = null!;
    public Guid SellerBaseFinancialAccountId { get; set; }
    public FinancialAccount SellerBaseFinancialAccount { get; set; } = null!;
    public Guid SellerQuoteFinancialAccountId { get; set; }
    public FinancialAccount SellerQuoteFinancialAccount { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal QuoteQuantity { get; set; }
    public TradeStatus Status { get; set; } = TradeStatus.PendingSettlement;

    public Guid? BaseLedgerTransactionId { get; set; }
    public LedgerTransaction? BaseLedgerTransaction { get; set; }
    public Guid? QuoteLedgerTransactionId { get; set; }
    public LedgerTransaction? QuoteLedgerTransaction { get; set; }

    public DateTime? SettlementStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
}
