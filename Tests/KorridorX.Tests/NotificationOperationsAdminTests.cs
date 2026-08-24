using KorridorX.Data;
using KorridorX.Models.Notifications;
using KorridorX.Services.Notifications;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class NotificationOperationsAdminTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public NotificationOperationsAdminTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Exhausted_notification_requires_attempt_reset_before_manual_retry()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service =
            scope.ServiceProvider.GetRequiredService<INotificationOperationsService>();

        var notification = NewDeadLetter(
            $"retry-exhausted-{Guid.NewGuid():N}@example.test");

        db.NotificationMessages.Add(notification);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RetryAsync(
                notification.Id,
                Guid.NewGuid(),
                resetAttemptCount: false,
                reason: "Manual recovery validation."));

        Assert.Contains(
            "exhausted its retry limit",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);

        var stored = await db.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == notification.Id);

        Assert.Equal(NotificationStatuses.DeadLetter, stored.Status);
        Assert.Equal(stored.MaxAttempts, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
    }

    [DatabaseIntegrationFact]
    public async Task Exhausted_notification_can_be_retried_when_attempt_count_is_reset()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service =
            scope.ServiceProvider.GetRequiredService<INotificationOperationsService>();

        var notification = NewDeadLetter(
            $"retry-reset-{Guid.NewGuid():N}@example.test");

        db.NotificationMessages.Add(notification);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.RetryAsync(
            notification.Id,
            Guid.NewGuid(),
            resetAttemptCount: true,
            reason: "Delivery provider recovered.");

        Assert.Equal(NotificationStatuses.Retry, result.Status);
        Assert.Equal(0, result.AttemptCount);
        Assert.NotNull(result.NextAttemptAt);
        Assert.Null(result.DeadLetteredAt);
        Assert.Null(result.ErrorMessage);

        var stored = await db.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == notification.Id);

        Assert.Equal(NotificationStatuses.Retry, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.NotNull(stored.NextAttemptAt);
        Assert.Null(stored.DeadLetteredAt);
    }

    private static NotificationMessage NewDeadLetter(string recipient) =>
        new()
        {
            Channel = NotificationChannels.Email,
            Recipient = recipient,
            Subject = "Notification retry exhaustion validation",
            Body = "Synthetic notification operations test.",
            Status = NotificationStatuses.DeadLetter,
            AttemptCount = 3,
            MaxAttempts = 3,
            NextAttemptAt = null,
            LastAttemptAt = DateTime.UtcNow.AddMinutes(-5),
            DeadLetteredAt = DateTime.UtcNow.AddMinutes(-4),
            ErrorMessage = "Synthetic provider failure.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
}