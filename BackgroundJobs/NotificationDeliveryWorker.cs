using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<NotificationDeliveryWorker> _logger;

    public NotificationDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationDeliveryOptions> options,
        ILogger<NotificationDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled)
        {
            _logger.LogInformation("Notification delivery worker is disabled.");
            return;
        }

        _logger.LogInformation("Notification delivery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification delivery batch failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<INotificationDeliveryProvider>();
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-_options.ProcessingTimeoutMinutes);

        var stale = await db.NotificationMessages
            .Where(x =>
                !x.IsDeleted &&
                x.Status == NotificationStatuses.Processing &&
                x.LockedAt != null &&
                x.LockedAt < staleBefore)
            .ToListAsync(ct);

        foreach (var item in stale)
        {
            item.LockedAt = null;
            item.LockId = null;
            item.LastUpdatedAt = now;

            if (item.AttemptCount >= item.MaxAttempts)
            {
                item.Status = NotificationStatuses.DeadLetter;
                item.DeadLetteredAt = now;
                item.NextAttemptAt = null;
                item.ErrorMessage = "Delivery exhausted its retry limit after a stale processing lock.";
            }
            else
            {
                item.Status = NotificationStatuses.Retry;
                item.NextAttemptAt = now;
                item.ErrorMessage = "Recovered after a stale processing lock.";
            }
        }

        if (stale.Count > 0)
            await db.SaveChangesAsync(ct);

        var dueIds = await db.NotificationMessages
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == NotificationStatuses.Pending || x.Status == NotificationStatuses.Retry) &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now) &&
                x.AttemptCount < x.MaxAttempts)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var notificationId in dueIds)
        {
            try
            {
                await ProcessOneAsync(db, provider, notificationId, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogDebug(
                    "Notification {NotificationId} was claimed or updated by another worker.",
                    notificationId);
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task ProcessOneAsync(
        AppDbContext db,
        INotificationDeliveryProvider provider,
        Guid notificationId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var lockId = Guid.NewGuid();
        var notification = await db.NotificationMessages
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                !x.IsDeleted &&
                (x.Status == NotificationStatuses.Pending || x.Status == NotificationStatuses.Retry),
                ct);

        if (notification is null)
            return;

        notification.Status = NotificationStatuses.Processing;
        notification.LockedAt = now;
        notification.LockId = lockId;
        notification.LastAttemptAt = now;
        notification.AttemptCount += 1;
        notification.LastUpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var result = await provider.SendAsync(notification, ct);
        var completedAt = DateTime.UtcNow;

        notification.LockedAt = null;
        notification.LockId = null;
        notification.LastUpdatedAt = completedAt;

        if (result.Success)
        {
            notification.Status = NotificationStatuses.Sent;
            notification.SentAt = completedAt;
            notification.ProviderMessageId = result.ProviderMessageId;
            notification.ErrorMessage = null;
            notification.NextAttemptAt = null;
        }
        else if (notification.AttemptCount >= notification.MaxAttempts)
        {
            notification.Status = NotificationStatuses.DeadLetter;
            notification.DeadLetteredAt = completedAt;
            notification.ErrorMessage = Truncate(result.ErrorMessage ?? "Notification delivery failed.", 1000);
            notification.NextAttemptAt = null;
        }
        else
        {
            notification.Status = NotificationStatuses.Retry;
            notification.ErrorMessage = Truncate(result.ErrorMessage ?? "Notification delivery failed.", 1000);
            notification.NextAttemptAt = NotificationDeliveryPolicy.CalculateNextAttemptAt(
                completedAt,
                notification.AttemptCount,
                _options.RetryBaseMinutes);
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
