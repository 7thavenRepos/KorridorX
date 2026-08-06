using KorridorX.Dtos.Notifications;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Notifications;

public interface INotificationOperationsService
{
    Task<PagedResult<NotificationMessageDto>> GetAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<NotificationMessageDto> RetryAsync(
        Guid notificationId,
        bool resetAttemptCount,
        CancellationToken ct = default);
}
