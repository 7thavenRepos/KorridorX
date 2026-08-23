using System.Reflection;
using KorridorX.BackgroundJobs;
using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class EmbeddedWebhookDeliveryWorkerTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public EmbeddedWebhookDeliveryWorkerTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Failed_delivery_moves_to_retry_with_exponential_backoff()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Pending,
            attemptCount: 0,
            maxAttempts: 5);

        var sender = new SequenceWebhookSender(
            new EmbeddedWebhookSendResult(
                false,
                503,
                "provider unavailable",
                "Webhook endpoint returned HTTP 503."));

        var firstBefore = DateTime.UtcNow;
        await InvokeProcessOneAsync(db, sender, deliveryId);
        var firstAfter = DateTime.UtcNow;

        db.ChangeTracker.Clear();

        var first = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.Retry, first.Status);
        Assert.Equal(1, first.AttemptCount);
        Assert.Null(first.LockId);
        Assert.Null(first.LockedAt);
        Assert.NotNull(first.NextAttemptAt);
        Assert.InRange(
            first.NextAttemptAt!.Value,
            firstBefore.AddSeconds(50),
            firstAfter.AddSeconds(70));

        var secondBefore = DateTime.UtcNow;
        await InvokeProcessOneAsync(db, sender, deliveryId);
        var secondAfter = DateTime.UtcNow;

        db.ChangeTracker.Clear();

        var second = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.Retry, second.Status);
        Assert.Equal(2, second.AttemptCount);
        Assert.NotNull(second.NextAttemptAt);
        Assert.InRange(
            second.NextAttemptAt!.Value,
            secondBefore.AddSeconds(110),
            secondAfter.AddSeconds(130));
    }

    [DatabaseIntegrationFact]
    public async Task Delivery_dead_letters_when_max_attempts_are_exhausted()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Retry,
            attemptCount: 1,
            maxAttempts: 2);

        var sender = new SequenceWebhookSender(
            new EmbeddedWebhookSendResult(
                false,
                500,
                "failure",
                "provider failure"));

        await InvokeProcessOneAsync(db, sender, deliveryId);

        db.ChangeTracker.Clear();

        var stored = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.DeadLetter, stored.Status);
        Assert.Equal(2, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Null(stored.LockId);
        Assert.Null(stored.LockedAt);
        Assert.Contains(
            "provider failure",
            stored.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Delivery_can_recover_and_complete_after_transient_failure()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Pending,
            attemptCount: 0,
            maxAttempts: 5);

        var sender = new SequenceWebhookSender(
            new EmbeddedWebhookSendResult(
                false,
                503,
                "temporarily unavailable",
                "temporary failure"),
            new EmbeddedWebhookSendResult(
                true,
                200,
                "ok",
                null));

        await InvokeProcessOneAsync(db, sender, deliveryId);
        await InvokeProcessOneAsync(db, sender, deliveryId);

        db.ChangeTracker.Clear();

        var stored = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.Delivered, stored.Status);
        Assert.Equal(2, stored.AttemptCount);
        Assert.NotNull(stored.DeliveredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Null(stored.ErrorMessage);
        Assert.Equal(200, stored.LastResponseStatusCode);
        Assert.Equal("ok", stored.LastResponseBody);
        Assert.Null(stored.LockId);
        Assert.Null(stored.LockedAt);
    }

    [DatabaseIntegrationFact]
    public async Task Disabled_endpoint_dead_letters_without_calling_sender()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Pending,
            attemptCount: 0,
            maxAttempts: 5,
            endpointStatus: BusinessWebhookEndpointStatus.Disabled);

        var sender = new SequenceWebhookSender(
            new EmbeddedWebhookSendResult(
                true,
                200,
                "should not be sent",
                null));

        await InvokeProcessOneAsync(db, sender, deliveryId);

        db.ChangeTracker.Clear();

        var stored = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.DeadLetter, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Equal("Webhook endpoint is disabled.", stored.ErrorMessage);
        Assert.Equal(0, sender.CallCount);
    }

    [DatabaseIntegrationFact]
    public async Task Worker_truncates_retry_error_messages_to_2000_characters()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Pending,
            attemptCount: 0,
            maxAttempts: 5);

        var sender = new SequenceWebhookSender(
            new EmbeddedWebhookSendResult(
                false,
                503,
                "failure",
                new string('e', 2500)));

        await InvokeProcessOneAsync(db, sender, deliveryId);

        db.ChangeTracker.Clear();

        var stored = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.Retry, stored.Status);
        Assert.NotNull(stored.ErrorMessage);
        Assert.Equal(2000, stored.ErrorMessage!.Length);
    }

    [DatabaseIntegrationFact]
    public async Task Stale_processing_lock_at_retry_limit_is_recovered_to_dead_letter()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deliveryId = await SeedDeliveryAsync(
            db,
            status: BusinessWebhookDeliveryStatus.Processing,
            attemptCount: 3,
            maxAttempts: 3,
            lockedAt: DateTime.UtcNow.AddMinutes(-11));

        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The integration-test database connection string was unavailable.");

        await using var isolatedProvider = BuildWorkerServiceProvider(
            connectionString,
            new SequenceWebhookSender(
                new EmbeddedWebhookSendResult(
                    true,
                    200,
                    "unused",
                    null)));

        var worker = new EmbeddedWebhookDeliveryWorker(
            isolatedProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmbeddedWebhookDeliveryWorker>.Instance);

        await InvokeProcessBatchAsync(worker);

        db.ChangeTracker.Clear();

        var stored = await db.BusinessWebhookDeliveries
            .AsNoTracking()
            .SingleAsync(x => x.Id == deliveryId);

        Assert.Equal(BusinessWebhookDeliveryStatus.DeadLetter, stored.Status);
        Assert.Equal(3, stored.AttemptCount);
        Assert.NotNull(stored.DeadLetteredAt);
        Assert.Null(stored.NextAttemptAt);
        Assert.Null(stored.LockId);
        Assert.Null(stored.LockedAt);
        Assert.Contains(
            "stale processing lock",
            stored.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> SeedDeliveryAsync(
        AppDbContext db,
        BusinessWebhookDeliveryStatus status,
        int attemptCount,
        int maxAttempts,
        BusinessWebhookEndpointStatus endpointStatus =
            BusinessWebhookEndpointStatus.Active,
        DateTime? lockedAt = null)
    {
        var unique = Guid.NewGuid().ToString("N");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"phase15b-worker-{unique}@example.test",
            NormalizedUserName =
                $"PHASE15B-WORKER-{unique}@EXAMPLE.TEST",
            Email = $"phase15b-worker-{unique}@example.test",
            NormalizedEmail =
                $"PHASE15B-WORKER-{unique}@EXAMPLE.TEST",
            EmailConfirmed = true,
            FirstName = "Phase",
            LastName = "Worker",
            CountryCode = "CA",
            UserType = UserType.Business,
            Status = UserStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        var profile = new BusinessProfile
        {
            OwnerUserId = user.Id,
            OwnerUser = user,
            BusinessName = $"Phase 15B Worker {unique}",
            CountryCode = "CA",
            ContactEmail = user.Email,
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var application = new ApiApplication
        {
            BusinessProfile = profile,
            BusinessProfileId = profile.Id,
            Name = $"Phase15BWorker-{unique}",
            Scopes = EmbeddedFinanceScope.WebhooksManage,
            Status = ApiApplicationStatus.Active
        };

        var endpoint = new BusinessWebhookEndpoint
        {
            BusinessProfileId = profile.Id,
            ApiApplication = application,
            ApiApplicationId = application.Id,
            Url = $"https://webhook.example.test/{unique}",
            EventTypesCsv = "transfer.completed",
            SigningSecretProtected = "unused-in-worker-tests",
            SigningSecretLastFour = "test",
            Status = endpointStatus,
            MaxAttempts = maxAttempts
        };

        var webhookEvent = new BusinessWebhookEvent
        {
            BusinessProfileId = profile.Id,
            EventId = $"evt_{unique}",
            EventType = "transfer.completed",
            PayloadJson =
                $$"""{"eventId":"evt_{{unique}}","type":"transfer.completed"}""",
            OccurredAt = DateTime.UtcNow
        };

        var delivery = new BusinessWebhookDelivery
        {
            BusinessWebhookEndpoint = endpoint,
            BusinessWebhookEndpointId = endpoint.Id,
            BusinessWebhookEvent = webhookEvent,
            BusinessWebhookEventId = webhookEvent.Id,
            Status = status,
            AttemptCount = attemptCount,
            NextAttemptAt = DateTime.UtcNow.AddHours(4),
            LockId = lockedAt.HasValue ? Guid.NewGuid() : null,
            LockedAt = lockedAt,
            LastAttemptAt = lockedAt,
            ErrorMessage = status == BusinessWebhookDeliveryStatus.Retry
                ? "previous failure"
                : null
        };

        db.Users.Add(user);
        db.BusinessProfiles.Add(profile);
        db.ApiApplications.Add(application);
        db.BusinessWebhookEndpoints.Add(endpoint);
        db.BusinessWebhookEvents.Add(webhookEvent);
        db.BusinessWebhookDeliveries.Add(delivery);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return delivery.Id;
    }

    private static async Task InvokeProcessOneAsync(
        AppDbContext db,
        IEmbeddedWebhookSender sender,
        Guid deliveryId)
    {
        var method = typeof(EmbeddedWebhookDeliveryWorker)
            .GetMethod(
                "ProcessOneAsync",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "EmbeddedWebhookDeliveryWorker.ProcessOneAsync was not found.");

        var task = method.Invoke(
            null,
            new object?[]
            {
                db,
                sender,
                deliveryId,
                CancellationToken.None
            }) as Task
            ?? throw new InvalidOperationException(
                "ProcessOneAsync did not return a Task.");

        await task;
    }

    private static async Task InvokeProcessBatchAsync(
        EmbeddedWebhookDeliveryWorker worker)
    {
        var method = typeof(EmbeddedWebhookDeliveryWorker)
            .GetMethod(
                "ProcessBatchAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "EmbeddedWebhookDeliveryWorker.ProcessBatchAsync was not found.");

        var task = method.Invoke(
            worker,
            new object?[] { CancellationToken.None }) as Task
            ?? throw new InvalidOperationException(
                "ProcessBatchAsync did not return a Task.");

        await task;
    }

    private static ServiceProvider BuildWorkerServiceProvider(
        string connectionString,
        IEmbeddedWebhookSender sender)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddSingleton(sender);
        services.AddSingleton<IEmbeddedWebhookSender>(sender);

        return services.BuildServiceProvider();
    }

    private sealed class SequenceWebhookSender : IEmbeddedWebhookSender
    {
        private readonly Queue<EmbeddedWebhookSendResult> _results;

        public SequenceWebhookSender(
            params EmbeddedWebhookSendResult[] results)
        {
            _results = new Queue<EmbeddedWebhookSendResult>(results);
        }

        public int CallCount { get; private set; }

        public Task<EmbeddedWebhookSendResult> SendAsync(
            Guid deliveryId,
            CancellationToken ct = default)
        {
            CallCount += 1;

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "The test webhook sender had no configured result.");
            }

            var result = _results.Count == 1
                ? _results.Peek()
                : _results.Dequeue();

            return Task.FromResult(result);
        }
    }
}
