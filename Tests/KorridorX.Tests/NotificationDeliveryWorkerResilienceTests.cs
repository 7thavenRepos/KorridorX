using System.Reflection;
using KorridorX.BackgroundJobs;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Notifications;
using KorridorX.Services.Notifications;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class NotificationDeliveryWorkerResilienceTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public NotificationDeliveryWorkerResilienceTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Provider_exception_moves_notification_to_retry_and_batch_continues()
    {
        Guid failedId;
        Guid successfulId;

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var failed = NewNotification(
                "failure@example.test",
                createdAt: DateTime.UnixEpoch.AddDays(20),
                attemptCount: 0,
                maxAttempts: 3);

            var successful = NewNotification(
                "success@example.test",
                createdAt: DateTime.UnixEpoch.AddDays(21),
                attemptCount: 0,
                maxAttempts: 3);

            db.NotificationMessages.AddRange(failed, successful);
            await db.SaveChangesAsync();

            failedId = failed.Id;
            successfulId = successful.Id;
        }

        var provider = new RecipientAwareProvider(
            recipient =>
            {
                if (recipient == "failure@example.test")
                    throw new HttpRequestException(
                        new string('x', 1200));

                return new NotificationDeliveryResult(
                    true,
                    ProviderMessageId: "msg-success");
            });

        await InvokeProcessBatchAsync(
            provider,
            batchSize: 2);

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var dbVerify =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedStored = await dbVerify.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == failedId);

        Assert.Equal(NotificationStatuses.Retry, failedStored.Status);
        Assert.Equal(1, failedStored.AttemptCount);
        Assert.Null(failedStored.LockId);
        Assert.Null(failedStored.LockedAt);
        Assert.NotNull(failedStored.NextAttemptAt);
        Assert.NotNull(failedStored.ErrorMessage);
        Assert.Equal(1000, failedStored.ErrorMessage!.Length);

        var successfulStored = await dbVerify.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == successfulId);

        Assert.Equal(NotificationStatuses.Sent, successfulStored.Status);
        Assert.Equal(1, successfulStored.AttemptCount);
        Assert.Equal("msg-success", successfulStored.ProviderMessageId);
        Assert.NotNull(successfulStored.SentAt);
        Assert.Null(successfulStored.LockId);
        Assert.Null(successfulStored.LockedAt);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_exception_on_final_attempt_dead_letters_immediately()
    {
        Guid notificationId;

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var notification = NewNotification(
                "deadletter@example.test",
                createdAt: DateTime.UnixEpoch.AddDays(22),
                attemptCount: 2,
                maxAttempts: 3);

            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();

            notificationId = notification.Id;
        }

        var provider = new RecipientAwareProvider(
            _ => throw new InvalidOperationException(
                "SMTP transport unavailable."));

        await InvokeProcessBatchAsync(
            provider,
            batchSize: 1);

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var dbVerify =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbVerify.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == notificationId);

        Assert.Equal(NotificationStatuses.DeadLetter, stored.Status);
        Assert.Equal(3, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Null(stored.LockId);
        Assert.Null(stored.LockedAt);
        Assert.Contains(
            "SMTP transport unavailable",
            stored.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Stale_processing_lock_at_retry_limit_dead_letters_without_resending()
    {
        Guid notificationId;

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var notification = NewNotification(
                "stale@example.test",
                createdAt: DateTime.UnixEpoch.AddDays(23),
                attemptCount: 3,
                maxAttempts: 3);

            notification.Status = NotificationStatuses.Processing;
            notification.LockId = Guid.NewGuid();
            notification.LockedAt = DateTime.UtcNow.AddMinutes(-15);
            notification.LastAttemptAt = notification.LockedAt;
            notification.NextAttemptAt = null;

            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();

            notificationId = notification.Id;
        }

        var provider = new RecipientAwareProvider(
            _ => new NotificationDeliveryResult(
                true,
                ProviderMessageId: "must-not-send"));

        await InvokeProcessBatchAsync(
            provider,
            batchSize: 1);

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var dbVerify =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbVerify.NotificationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == notificationId);

        Assert.Equal(NotificationStatuses.DeadLetter, stored.Status);
        Assert.Equal(3, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Null(stored.LockId);
        Assert.Null(stored.LockedAt);
        Assert.Contains(
            "stale processing lock",
            stored.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        // The RC integration database is shared. The worker may send
        // other due notifications after recovering this row. Verify that the
        // stale exhausted notification itself was never resent.
        Assert.DoesNotContain(
            "stale@example.test",
            provider.Recipients);
    }

    private async Task InvokeProcessBatchAsync(
        INotificationDeliveryProvider provider,
        int batchSize)
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connectionString =
            db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The integration-test database connection string was unavailable.");

        await using var workerProvider =
            BuildWorkerServiceProvider(
                connectionString,
                provider);

        var options = Options.Create(
            new NotificationDeliveryOptions
            {
                WorkerEnabled = true,
                PollIntervalSeconds = 1,
                BatchSize = batchSize,
                MaxAttempts = 3,
                RetryBaseMinutes = 2,
                ProcessingTimeoutMinutes = 10
            });

        var worker = new NotificationDeliveryWorker(
            workerProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<NotificationDeliveryWorker>.Instance);

        var method = typeof(NotificationDeliveryWorker)
            .GetMethod(
                "ProcessBatchAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "NotificationDeliveryWorker.ProcessBatchAsync was not found.");

        var task = method.Invoke(
            worker,
            new object?[] { CancellationToken.None }) as Task
            ?? throw new InvalidOperationException(
                "ProcessBatchAsync did not return a Task.");

        await task;
    }

    private static ServiceProvider BuildWorkerServiceProvider(
        string connectionString,
        INotificationDeliveryProvider provider)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddSingleton(provider);
        services.AddSingleton<INotificationDeliveryProvider>(provider);

        return services.BuildServiceProvider();
    }

    private static NotificationMessage NewNotification(
        string recipient,
        DateTime createdAt,
        int attemptCount,
        int maxAttempts) =>
        new()
        {
            Channel = NotificationChannels.Email,
            Recipient = recipient,
            Subject = "Phase 15B notification resilience",
            Body = "Notification worker resilience validation.",
            Status = NotificationStatuses.Pending,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = createdAt
        };

    private sealed class RecipientAwareProvider
        : INotificationDeliveryProvider
    {
        private readonly Func<string, NotificationDeliveryResult> _send;

        public RecipientAwareProvider(
            Func<string, NotificationDeliveryResult> send)
        {
            _send = send;
        }

        public int CallCount { get; private set; }

        public List<string> Recipients { get; } = new();

        public Task<NotificationDeliveryResult> SendAsync(
            NotificationMessage notification,
            CancellationToken ct = default)
        {
            CallCount += 1;
            Recipients.Add(notification.Recipient);
            return Task.FromResult(_send(notification.Recipient));
        }
    }
}
