using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Recipients;

namespace KorridorX.Models.Transfers;

public class Transfer : AuditableEntity
{
    public string Reference { get; set; } = "";

    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }

    public Guid? RecipientId { get; set; }
    public Recipient? Recipient { get; set; }

    public Guid? BusinessBeneficiaryId { get; set; }
    public BusinessBeneficiary? BusinessBeneficiary { get; set; }

    public Guid? RecipientBankAccountId { get; set; }
    public RecipientBankAccount? RecipientBankAccount { get; set; }

    public Guid? RecipientMobileWalletId { get; set; }
    public RecipientMobileWallet? RecipientMobileWallet { get; set; }

    public Guid? BusinessBeneficiaryBankAccountId { get; set; }
    public BusinessBeneficiaryBankAccount? BusinessBeneficiaryBankAccount { get; set; }

    public Guid? BusinessBeneficiaryMobileWalletId { get; set; }
    public BusinessBeneficiaryMobileWallet? BusinessBeneficiaryMobileWallet { get; set; }

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

    public BusinessFundingSource? BusinessFundingSource { get; set; }
    public BusinessApprovalStatus ApprovalStatus { get; set; } = BusinessApprovalStatus.NotRequired;
    public int RequiredApprovals { get; set; }
    public int ApprovalCount { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? SubmittedForApprovalAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? FinalApprovedByUserId { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? ApprovalRejectionReason { get; set; }

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderTransferId { get; set; }
    public string? ProviderReference { get; set; }

    public DateTime? PaymentReceivedAt { get; set; }
    public DateTime? PayoutInitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? FailureReason { get; set; }

    public int RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public RiskDecision RiskDecision { get; set; } = RiskDecision.Allow;
    public bool IsComplianceHold { get; set; }
    public string? ComplianceHoldReason { get; set; }
    public DateTime? RiskAssessedAt { get; set; }
    public DateTime? ComplianceReviewedAt { get; set; }
    public Guid? ComplianceReviewedByUserId { get; set; }

    public ICollection<TransferStatusHistory> StatusHistories { get; set; } = new List<TransferStatusHistory>();
    public ICollection<TransferTimelineEvent> TimelineEvents { get; set; } = new List<TransferTimelineEvent>();
}
