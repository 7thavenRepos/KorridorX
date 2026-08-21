using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Models.Instant;

public class InstantTrade : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid InstantQuoteId { get; set; }
    public InstantQuote InstantQuote { get; set; } = null!;

    public Guid InstantPairId { get; set; }
    public InstantPair InstantPair { get; set; } = null!;

    // UserId is retained for backwards compatibility with the consumer instant API.
    // OwnerType/OwnerId are the canonical ownership boundary for new flows.
    public Guid UserId { get; set; }

    public FinancialAccountOwnerType OwnerType { get; set; } = FinancialAccountOwnerType.User;
    public Guid OwnerId { get; set; }

    public Guid UserSourceFinancialAccountId { get; set; }
    public FinancialAccount UserSourceFinancialAccount { get; set; } = null!;

    public Guid UserDestinationFinancialAccountId { get; set; }
    public FinancialAccount UserDestinationFinancialAccount { get; set; } = null!;

    public Guid HouseSourceFinancialAccountId { get; set; }
    public FinancialAccount HouseSourceFinancialAccount { get; set; } = null!;

    public Guid HouseDestinationFinancialAccountId { get; set; }
    public FinancialAccount HouseDestinationFinancialAccount { get; set; } = null!;

    public Guid? ReservationId { get; set; }
    public FinancialReservation? Reservation { get; set; }

    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }
    public decimal CustomerRate { get; set; }

    public InstantTradeStatus Status { get; set; } = InstantTradeStatus.PendingSettlement;

    public Guid? SourceLedgerTransactionId { get; set; }
    public LedgerTransaction? SourceLedgerTransaction { get; set; }

    public Guid? DestinationLedgerTransactionId { get; set; }
    public LedgerTransaction? DestinationLedgerTransaction { get; set; }

    public DateTime? SettlementStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
}
