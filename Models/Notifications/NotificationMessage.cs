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

    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public Guid? LockId { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public string? ProviderMessageId { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
}