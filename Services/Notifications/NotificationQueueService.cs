using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Notifications;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

public class NotificationQueueService : INotificationQueueService
{
    private readonly AppDbContext _db;
    private readonly NotificationDeliveryOptions _options;

    public NotificationQueueService(
        AppDbContext db,
        IOptions<NotificationDeliveryOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task QueueBusinessAsync(
        Guid businessProfileId,
        string subject,
        string body,
        string relatedEntityType,
        Guid relatedEntityId,
        CancellationToken ct = default)
    {
        var business = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessProfileId && !x.IsDeleted)
            .Select(x => new
            {
                x.OwnerUserId,
                OwnerEmail = x.OwnerUser.Email
            })
            .FirstOrDefaultAsync(ct);

        if (business is null)
            return;

        var members = await _db.BusinessUsers
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => new { x.UserId, Email = x.User.Email })
            .ToListAsync(ct);

        var recipients = new Dictionary<Guid, string?>
        {
            [business.OwnerUserId] = business.OwnerEmail
        };

        foreach (var member in members)
            recipients[member.UserId] = member.Email;

        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            AddEmail(
                recipient.Key,
                recipient.Value!,
                subject,
                body,
                relatedEntityType,
                relatedEntityId.ToString());
        }
    }

    public async Task QueueUserAsync(
        Guid userId,
        string subject,
        string body,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken ct = default)
    {
        var email = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(email))
            return;

        AddEmail(
            userId,
            email,
            subject,
            body,
            relatedEntityType,
            relatedEntityId?.ToString());
    }

    public Task QueueEmailAsync(
        string email,
        string subject,
        string body,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Notification email is required.");

        AddEmail(
            null,
            email.Trim().ToLowerInvariant(),
            subject,
            body,
            relatedEntityType,
            relatedEntityId?.ToString());
        return Task.CompletedTask;
    }

    public async Task<PagedResult<NotificationMessageDto>> GetMyNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _db.NotificationMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
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

    public async Task<NotificationMessageDto> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken ct = default)
    {
        var notification = await _db.NotificationMessages
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.UserId == userId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Notification not found.");

        notification.ReadAt ??= DateTime.UtcNow;
        notification.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(notification);
    }

    private void AddEmail(
        Guid? userId,
        string email,
        string subject,
        string body,
        string? relatedEntityType,
        string? relatedEntityId)
    {
        _db.NotificationMessages.Add(new NotificationMessage
        {
            UserId = userId,
            Channel = NotificationChannels.Email,
            Recipient = email,
            Subject = Truncate(subject, 255),
            Body = body,
            Status = NotificationStatuses.Pending,
            AttemptCount = 0,
            MaxAttempts = _options.MaxAttempts,
            NextAttemptAt = DateTime.UtcNow,
            RelatedEntityType = TruncateNullable(relatedEntityType, 100),
            RelatedEntityId = TruncateNullable(relatedEntityId, 100)
        });
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

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
