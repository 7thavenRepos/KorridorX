using KorridorX.Models.Common;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Support;

public class SupportEvidence : BaseEntity
{
    public Guid? SupportTicketId { get; set; }
    public SupportTicket? SupportTicket { get; set; }
    public Guid? TransferDisputeId { get; set; }
    public TransferDispute? TransferDispute { get; set; }
    public Guid? TransferInvestigationId { get; set; }
    public TransferInvestigation? TransferInvestigation { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public ApplicationUser SubmittedByUser { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? MimeType { get; set; }
    public string StorageKey { get; set; } = "";
    public string? StorageUrl { get; set; }
}
