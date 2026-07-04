using KorridorX.Models.Common;

namespace KorridorX.Models.Notifications;

public class NotificationMessage : BaseEntity
{
    public Guid? UserId { get; set; }

    public string Channel { get; set; } = "";
    public string Recipient { get; set; } = "";

    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    public string Status { get; set; } = "Pending";

    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
}