using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Payments;

public class Payout : AuditableEntity
{
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;

    public string Reference { get; set; } = "";

    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderPayoutId { get; set; }
    public string? ProviderReference { get; set; }

    public DateTime? InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<PayoutAttempt> Attempts { get; set; } = new List<PayoutAttempt>();
}