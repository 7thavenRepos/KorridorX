using KorridorX.Dtos.Notifications;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Notifications;

public interface INotificationOperationsService
{
    Task<PagedResult<NotificationMessageDto>> GetAsync(
        string? status,
        string? channel,
        string? search,
        string? relatedEntityType,
        string? relatedEntityId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<NotificationMessageDto> GetByIdAsync(
        Guid notificationId,
        CancellationToken ct = default);

    Task<NotificationMessageDto> RetryAsync(
        Guid notificationId,
        Guid initiatedByUserId,
        bool resetAttemptCount,
        string reason,
        CancellationToken ct = default);
}