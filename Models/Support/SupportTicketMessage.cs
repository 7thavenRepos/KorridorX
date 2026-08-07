using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Support;

public class SupportTicketMessage : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;
    public Guid? AuthorUserId { get; set; }
    public ApplicationUser? AuthorUser { get; set; }
    public SupportMessageAuthorType AuthorType { get; set; }
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
}
