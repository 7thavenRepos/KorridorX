using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Recipients;

namespace KorridorX.Models.Transfers;

public class Transfer : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public Guid RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;

    public Guid? RecipientBankAccountId { get; set; }
    public RecipientBankAccount? RecipientBankAccount { get; set; }

    public Guid? RecipientMobileWalletId { get; set; }
    public RecipientMobileWallet? RecipientMobileWallet { get; set; }

    public Guid TransferQuoteId { get; set; }
    public TransferQuote TransferQuote { get; set; } = null!;

    public TransferType TransferType { get; set; }
    public TransferPurpose Purpose { get; set; }

    public string? PurposeNote { get; set; }

    public string SourceCountryCode { get; set; } = "";
    public string DestinationCountryCode { get; set; } = "";

    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";

    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }

    public decimal FeeAmount { get; set; }
    public string FeeCurrencyCode { get; set; } = "";

    public decimal TotalPayableAmount { get; set; }

    public decimal CustomerRate { get; set; }
    public decimal ProviderRate { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Draft;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderTransferId { get; set; }
    public string? ProviderReference { get; set; }

    public DateTime? PaymentReceivedAt { get; set; }
    public DateTime? PayoutInitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<TransferStatusHistory> StatusHistories { get; set; } = new List<TransferStatusHistory>();
    public ICollection<TransferTimelineEvent> TimelineEvents { get; set; } = new List<TransferTimelineEvent>();
}