using KorridorX.Models.Common;

namespace KorridorX.Models.AdminFunding;

public enum AdminWalletFundingStatus
{
    PendingApproval = 1,
    Posted = 2,
    Rejected = 3
}

/// <summary>
/// Administrator-initiated, house-account-backed funding request.
/// Operations requests are inert until an independent Admin/SuperAdmin approves.
/// </summary>
public sealed class AdminWalletFundingRequest : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid SourceHouseAccountId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public string AssetCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public AdminWalletFundingStatus Status { get; set; } = AdminWalletFundingStatus.PendingApproval;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
}
