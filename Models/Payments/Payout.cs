using KorridorX.Models.FinancialCore;
using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Payments;

public class Payout : AuditableEntity
{
    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public PaymentOperationPurpose Purpose { get; set; } = PaymentOperationPurpose.Remittance;

    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }

    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    public string? ContextEntityType { get; set; }
    public Guid? ContextEntityId { get; set; }
    public string Reference { get; set; } = "";

    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderPayoutId { get; set; }
    public string? ProviderReference { get; set; }

    public string? InteracQuestion { get; set; }
    public string? InteracAnswer { get; set; }

    public DateTime? InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<PayoutAttempt> Attempts { get; set; } = new List<PayoutAttempt>();
}