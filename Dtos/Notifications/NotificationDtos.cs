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

public sealed class RegisterMobilePushDeviceRequestDto
{
    [Required]
    [MaxLength(20)]
    public string Platform { get; set; } =
        string.Empty;

    [Required]
    [MaxLength(4096)]
    public string PushToken { get; set; } =
        string.Empty;

    [Required]
    [MaxLength(200)]
    public string DeviceFingerprint { get; set; } =
        string.Empty;

    [Required]
    [MaxLength(200)]
    public string DeviceName { get; set; } =
        string.Empty;
}

public sealed class UnregisterMobilePushDeviceRequestDto
{
    [Required]
    [MaxLength(4096)]
    public string PushToken { get; set; } =
        string.Empty;
}

public record MobilePushDeviceDto(
    Guid Id,
    string Platform,
    string DeviceName,
    bool IsActive,
    DateTime LastSeenAt);

public record UnregisterMobilePushDeviceResultDto(
    bool Unregistered);
