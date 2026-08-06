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

public record RetryNotificationRequestDto(bool ResetAttemptCount = false);
