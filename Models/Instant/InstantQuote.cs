using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Fx;

namespace KorridorX.Models.Instant;

public class InstantQuote : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid InstantPairId { get; set; }
    public InstantPair InstantPair { get; set; } = null!;

    public Guid UserId { get; set; }

    public Guid UserSourceFinancialAccountId { get; set; }
    public FinancialAccount UserSourceFinancialAccount { get; set; } = null!;

    public Guid UserDestinationFinancialAccountId { get; set; }
    public FinancialAccount UserDestinationFinancialAccount { get; set; } = null!;

    public Guid HouseSourceFinancialAccountId { get; set; }
    public FinancialAccount HouseSourceFinancialAccount { get; set; } = null!;

    public Guid HouseDestinationFinancialAccountId { get; set; }
    public FinancialAccount HouseDestinationFinancialAccount { get; set; } = null!;

    public Guid ExchangeRateId { get; set; }
    public ExchangeRate ExchangeRate { get; set; } = null!;

    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }
    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }

    public InstantQuoteStatus Status { get; set; } = InstantQuoteStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public InstantTrade? Trade { get; set; }
}
