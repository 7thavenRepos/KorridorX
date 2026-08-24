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
        string? channel,
        string? search,
        string? relatedEntityType,
        string? relatedEntityId,
        DateTime? from,
        DateTime? to,
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

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalized = channel.Trim();
            query = query.Where(x => x.Channel == normalized);
        }

        if (!string.IsNullOrWhiteSpace(relatedEntityType))
        {
            var normalized = relatedEntityType.Trim();
            query = query.Where(x => x.RelatedEntityType == normalized);
        }

        if (!string.IsNullOrWhiteSpace(relatedEntityId))
        {
            var normalized = relatedEntityId.Trim();
            query = query.Where(x => x.RelatedEntityId == normalized);
        }

        if (from.HasValue)
        {
            var value = from.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAt >= value);
        }

        if (to.HasValue)
        {
            var value = to.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAt <= value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Recipient.ToLower().Contains(value) ||
                x.Subject.ToLower().Contains(value) ||
                (x.ProviderMessageId != null &&
                 x.ProviderMessageId.ToLower().Contains(value)) ||
                (x.RelatedEntityType != null &&
                 x.RelatedEntityType.ToLower().Contains(value)) ||
                (x.RelatedEntityId != null &&
                 x.RelatedEntityId.ToLower().Contains(value)) ||
                (x.ErrorMessage != null &&
                 x.ErrorMessage.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationMessageDto(
                x.Id,
                x.Channel,
                x.Recipient,
                x.Subject,
                x.RelatedEntityType == NotificationSecurityPolicy.AccountSecurityEntityType
                    ? NotificationSecurityPolicy.RedactedBody
                    : x.Body,
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

    public async Task<NotificationMessageDto> GetByIdAsync(
        Guid notificationId,
        CancellationToken ct = default)
    {
        var notification = await _db.NotificationMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Notification not found.");

        return ToDto(notification);
    }

    public async Task<NotificationMessageDto> RetryAsync(
        Guid notificationId,
        Guid initiatedByUserId,
        bool resetAttemptCount,
        string reason,
        CancellationToken ct = default)
    {
        var cleanedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(cleanedReason))
            throw new InvalidOperationException(
                "A reason is required to retry a notification.");

        if (cleanedReason.Length > 1000)
            throw new InvalidOperationException(
                "Reason cannot exceed 1000 characters.");

        var notification = await _db.NotificationMessages
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Notification not found.");

        if (notification.Status == NotificationStatuses.Sent)
            throw new InvalidOperationException(
                "A sent notification cannot be retried.");

        if (notification.Status == NotificationStatuses.Processing)
            throw new InvalidOperationException(
                "A notification currently being processed cannot be retried.");

        if (!resetAttemptCount &&
            notification.MaxAttempts > 0 &&
            notification.AttemptCount >= notification.MaxAttempts)
        {
            throw new InvalidOperationException(
                "The notification has exhausted its retry limit. Reset the attempt count before retrying.");
        }

        var old = new
        {
            notification.Status,
            notification.AttemptCount,
            notification.MaxAttempts,
            notification.NextAttemptAt,
            notification.DeadLetteredAt,
            notification.ErrorMessage
        };

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
            OldValues: old,
            NewValues: new
            {
                notification.Status,
                notification.AttemptCount,
                notification.MaxAttempts,
                notification.NextAttemptAt,
                notification.DeadLetteredAt,
                notification.ErrorMessage
            },
            Metadata: new
            {
                ResetAttemptCount = resetAttemptCount,
                Reason = cleanedReason
            },
            UserId: initiatedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDto(notification);
    }

    private static NotificationMessageDto ToDto(NotificationMessage x) =>
        new(
            x.Id,
            x.Channel,
            x.Recipient,
            x.Subject,
            NotificationSecurityPolicy.BodyForApi(x),
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