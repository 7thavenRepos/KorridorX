using KorridorX.Dtos.Notifications;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Notifications;

public interface INotificationQueueService
{
    Task QueueBusinessAsync(
        Guid businessProfileId,
        string subject,
        string body,
        string relatedEntityType,
        Guid relatedEntityId,
        CancellationToken ct = default);

    Task QueueUserAsync(
        Guid userId,
        string subject,
        string body,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken ct = default);

    Task<PagedResult<NotificationMessageDto>> GetMyNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<NotificationMessageDto> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken ct = default);
}
