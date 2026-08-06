using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Payments;

public class Collection : AuditableEntity
{
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;

    public string Reference { get; set; } = "";

    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public CollectionStatus Status { get; set; } = CollectionStatus.Pending;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderCollectionId { get; set; }
    public string? ProviderReference { get; set; }

    public string? CheckoutUrl { get; set; }
    public DateTime? ProviderExpiresAt { get; set; }
    public string? VirtualAccountNumber { get; set; }
    public string? VirtualAccountBankName { get; set; }
    public string? VirtualAccountName { get; set; }

    public DateTime? InitiatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? RefundInitiatedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public string? ProviderRefundId { get; set; }
    public string? ProviderRefundReference { get; set; }
    public string? RefundReason { get; set; }
    public string? RefundFailureReason { get; set; }
    public DateTime? LastRefundSyncedAt { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<CollectionAttempt> Attempts { get; set; } = new List<CollectionAttempt>();
}