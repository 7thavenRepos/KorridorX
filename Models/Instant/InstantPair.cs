using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.Instant;

public class InstantPair : AuditableEntity
{
    public string Code { get; set; } = "";

    public string SourceAssetCode { get; set; } = "";
    public Asset SourceAsset { get; set; } = null!;

    public string DestinationAssetCode { get; set; } = "";
    public Asset DestinationAsset { get; set; } = null!;

    public Guid HouseSourceFinancialAccountId { get; set; }
    public FinancialAccount HouseSourceFinancialAccount { get; set; } = null!;

    public Guid HouseDestinationFinancialAccountId { get; set; }
    public FinancialAccount HouseDestinationFinancialAccount { get; set; } = null!;

    public InstantPairStatus Status { get; set; } = InstantPairStatus.Paused;

    public decimal MinimumSourceAmount { get; set; }
    public decimal? MaximumSourceAmount { get; set; }
    public decimal SourceAmountIncrement { get; set; }
    public int QuoteValiditySeconds { get; set; } = 30;

    public ICollection<InstantQuote> Quotes { get; set; } = new List<InstantQuote>();
    public ICollection<InstantTrade> Trades { get; set; } = new List<InstantTrade>();
}
