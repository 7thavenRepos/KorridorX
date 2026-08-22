using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Marketplace;

public class BusinessTradingRfqQuote : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid BusinessTradingRfqId { get; set; }
    public BusinessTradingRfq BusinessTradingRfq { get; set; } = null!;
    public FinancialAccountOwnerType ResponderOwnerType { get; set; }
    public Guid ResponderOwnerId { get; set; }
    public decimal Price { get; set; }
    public BusinessTradingRfqQuoteStatus Status { get; set; } = BusinessTradingRfqQuoteStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
}
