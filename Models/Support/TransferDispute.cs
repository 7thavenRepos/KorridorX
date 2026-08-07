using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Support;

public class TransferDispute : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }
    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public TransferDisputeType DisputeType { get; set; }
    public TransferDisputeStatus Status { get; set; } = TransferDisputeStatus.Open;
    public string Reason { get; set; } = "";
    public decimal? RequestedRefundAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public ICollection<TransferInvestigation> Investigations { get; set; } = new List<TransferInvestigation>();
    public ICollection<SupportEvidence> Evidence { get; set; } = new List<SupportEvidence>();
}
