using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Notifications;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Notifications;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Notifications;

public sealed class NotificationOperationsService : INotificationOperationsService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public NotificationOperationsService(
        AppDbContext db,
        IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<PagedResult<NotificationMessageDto>> GetAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.NotificationMessages
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            query = query.Where(x => x.Status == normalized);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationMessageDto(
                x.Id,
                x.Channel,
                x.Recipient,
                x.Subject,
                x.Body,
                x.Status,
                x.AttemptCount,
                x.MaxAttempts,
                x.NextAttemptAt,
                x.LastAttemptAt,
                x.DeadLetteredAt,
                x.ProviderMessageId,
                x.ErrorMessage,
                x.RelatedEntityType,
                x.RelatedEntityId,
                x.CreatedAt,
                x.SentAt,
                x.ReadAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<NotificationMessageDto> RetryAsync(
        Guid notificationId,
        bool resetAttemptCount,
        CancellationToken ct = default)
    {
        var notification = await _db.NotificationMessages
            .FirstOrDefaultAsync(x => x.Id == notificationId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Notification not found.");

        if (notification.Status == NotificationStatuses.Sent)
            throw new InvalidOperationException("A sent notification cannot be retried.");
        if (notification.Status == NotificationStatuses.Processing)
            throw new InvalidOperationException("A notification currently being processed cannot be retried.");

        notification.Status = NotificationStatuses.Retry;
        notification.NextAttemptAt = DateTime.UtcNow;
        notification.LockedAt = null;
        notification.LockId = null;
        notification.DeadLetteredAt = null;
        notification.ErrorMessage = null;
        notification.LastUpdatedAt = DateTime.UtcNow;

        if (resetAttemptCount)
            notification.AttemptCount = 0;
        if (notification.MaxAttempts <= 0)
            notification.MaxAttempts = 5;

        _auditService.Stage(new AuditRecordRequest(
            Action: "NOTIFICATION_RETRY_QUEUED",
            Category: "Notifications",
            EntityName: nameof(NotificationMessage),
            EntityId: notification.Id.ToString(),
            NewValues: new
            {
                notification.Status,
                notification.AttemptCount,
                notification.NextAttemptAt
            },
            Metadata: new { resetAttemptCount }));

        await _db.SaveChangesAsync(ct);
        return ToDto(notification);
    }

    private static NotificationMessageDto ToDto(NotificationMessage x) =>
        new(
            x.Id,
            x.Channel,
            x.Recipient,
            x.Subject,
            x.Body,
            x.Status,
            x.AttemptCount,
            x.MaxAttempts,
            x.NextAttemptAt,
            x.LastAttemptAt,
            x.DeadLetteredAt,
            x.ProviderMessageId,
            x.ErrorMessage,
            x.RelatedEntityType,
            x.RelatedEntityId,
            x.CreatedAt,
            x.SentAt,
            x.ReadAt);
}
