using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Notifications;

public record NotificationMessageDto(
    Guid Id,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime? NextAttemptAt,
    DateTime? LastAttemptAt,
    DateTime? DeadLetteredAt,
    string? ProviderMessageId,
    string? ErrorMessage,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReadAt);

public sealed class RetryNotificationRequestDto
{
    public bool ResetAttemptCount { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}