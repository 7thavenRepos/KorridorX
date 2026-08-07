using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Support;

public class SupportTicket : AuditableEntity
{
    public string Reference { get; set; } = "";
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }
    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }
    public SupportTicketCategory Category { get; set; } = SupportTicketCategory.General;
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public string Subject { get; set; } = "";
    public string Summary { get; set; } = "";
    public Guid? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public DateTime FirstResponseDueAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastCustomerMessageAt { get; set; }
    public DateTime? LastAgentMessageAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAt { get; set; }
    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
    public ICollection<SupportEvidence> Evidence { get; set; } = new List<SupportEvidence>();
}
