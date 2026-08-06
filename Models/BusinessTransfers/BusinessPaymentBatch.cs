using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.BusinessTransfers;

public class BusinessPaymentBatch : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string? OriginalFileName { get; set; }

    public string SourceCountryCode { get; set; } = "";
    public string SourceCurrencyCode { get; set; } = "";
    public BusinessFundingSource FundingSource { get; set; } = BusinessFundingSource.BusinessWallet;
    public BusinessPaymentBatchStatus Status { get; set; } = BusinessPaymentBatchStatus.Draft;

    public int TotalItems { get; set; }
    public int ValidItems { get; set; }
    public int InvalidItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }

    public decimal TotalSourceAmount { get; set; }
    public decimal TotalFeeAmount { get; set; }
    public decimal TotalPayableAmount { get; set; }

    public int RequiredApprovals { get; set; } = 1;
    public int ApprovalCount { get; set; }

    public DateTime? ValidatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? RejectionReason { get; set; }
    public string? FailureReason { get; set; }

    public ICollection<BusinessPaymentBatchItem> Items { get; set; } = new List<BusinessPaymentBatchItem>();
    public ICollection<BusinessApproval> Approvals { get; set; } = new List<BusinessApproval>();
}
