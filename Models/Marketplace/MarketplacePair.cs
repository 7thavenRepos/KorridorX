using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.Marketplace;

public class MarketplacePair : AuditableEntity
{
    public string Code { get; set; } = "";
    public string BaseAssetCode { get; set; } = "";
    public Asset BaseAsset { get; set; } = null!;
    public string QuoteAssetCode { get; set; } = "";
    public Asset QuoteAsset { get; set; } = null!;

    public MarketplacePairStatus Status { get; set; } = MarketplacePairStatus.Active;
    public decimal MinimumOrderQuantity { get; set; }
    public decimal? MaximumOrderQuantity { get; set; }
    public decimal QuantityIncrement { get; set; }
    public decimal PriceIncrement { get; set; }

    public ICollection<TradeOrder> Orders { get; set; } = new List<TradeOrder>();
    public ICollection<TradeMatch> Matches { get; set; } = new List<TradeMatch>();
}
