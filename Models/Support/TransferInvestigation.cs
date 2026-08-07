using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Support;

public class TransferInvestigation : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;
    public Guid? TransferDisputeId { get; set; }
    public TransferDispute? TransferDispute { get; set; }
    public Guid? SupportTicketId { get; set; }
    public SupportTicket? SupportTicket { get; set; }
    public TransferInvestigationStatus Status { get; set; } = TransferInvestigationStatus.Open;
    public TransferInvestigationOutcome Outcome { get; set; } = TransferInvestigationOutcome.None;
    public Guid? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public string Summary { get; set; } = "";
    public string? Findings { get; set; }
    public string? ProviderCaseReference { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public ICollection<SupportEvidence> Evidence { get; set; } = new List<SupportEvidence>();
}
