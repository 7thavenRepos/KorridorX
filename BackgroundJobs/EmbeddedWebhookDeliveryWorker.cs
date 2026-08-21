using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.BackgroundJobs;

public sealed class EmbeddedWebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddedWebhookDeliveryWorker> _logger;

    public EmbeddedWebhookDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<EmbeddedWebhookDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedded Finance webhook delivery worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Embedded Finance webhook delivery batch failed."); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmbeddedWebhookSender>();
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-10);

        var stale = await db.BusinessWebhookDeliveries.Include(x => x.BusinessWebhookEndpoint)
            .Where(x => x.Status == BusinessWebhookDeliveryStatus.Processing && x.LockedAt != null && x.LockedAt < staleBefore)
            .ToListAsync(ct);

        foreach (var item in stale)
        {
            item.LockedAt = null; item.LockId = null; item.LastUpdatedAt = now;
            if (item.AttemptCount >= item.BusinessWebhookEndpoint.MaxAttempts)
            {
                item.Status = BusinessWebhookDeliveryStatus.DeadLetter;
                item.DeadLetteredAt = now;
                item.NextAttemptAt = null;
                item.ErrorMessage = "Delivery exhausted its retry limit after a stale processing lock.";
            }
            else
            {
                item.Status = BusinessWebhookDeliveryStatus.Retry;
                item.NextAttemptAt = now;
                item.ErrorMessage = "Recovered after a stale processing lock.";
            }
        }
        if (stale.Count > 0) await db.SaveChangesAsync(ct);

        var dueIds = await db.BusinessWebhookDeliveries.AsNoTracking()
            .Where(x =>
                (x.Status == BusinessWebhookDeliveryStatus.Pending || x.Status == BusinessWebhookDeliveryStatus.Retry) &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(20).ToListAsync(ct);

        foreach (var id in dueIds)
        {
            try { await ProcessOneAsync(db, sender, id, ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
        }
    }

    private static async Task ProcessOneAsync(AppDbContext db, IEmbeddedWebhookSender sender, Guid deliveryId, CancellationToken ct)
    {
        var delivery = await db.BusinessWebhookDeliveries.Include(x => x.BusinessWebhookEndpoint)
            .FirstOrDefaultAsync(x =>
                x.Id == deliveryId &&
                (x.Status == BusinessWebhookDeliveryStatus.Pending || x.Status == BusinessWebhookDeliveryStatus.Retry), ct);
        if (delivery is null) return;

        var now = DateTime.UtcNow;
        if (delivery.BusinessWebhookEndpoint.Status != BusinessWebhookEndpointStatus.Active || delivery.BusinessWebhookEndpoint.IsDeleted)
        {
            delivery.Status = BusinessWebhookDeliveryStatus.DeadLetter;
            delivery.DeadLetteredAt = now;
            delivery.ErrorMessage = "Webhook endpoint is disabled.";
            delivery.NextAttemptAt = null;
            delivery.LastUpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return;
        }

        delivery.Status = BusinessWebhookDeliveryStatus.Processing;
        delivery.LockId = Guid.NewGuid();
        delivery.LockedAt = now;
        delivery.LastAttemptAt = now;
        delivery.AttemptCount += 1;
        delivery.LastUpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var result = await sender.SendAsync(delivery.Id, ct);
        var completedAt = DateTime.UtcNow;
        delivery.LockId = null;
        delivery.LockedAt = null;
        delivery.LastUpdatedAt = completedAt;
        delivery.LastResponseStatusCode = result.StatusCode;
        delivery.LastResponseBody = Truncate(result.ResponseBody, 4000);

        if (result.Success)
        {
            delivery.Status = BusinessWebhookDeliveryStatus.Delivered;
            delivery.DeliveredAt = completedAt;
            delivery.NextAttemptAt = null;
            delivery.ErrorMessage = null;
        }
        else if (delivery.AttemptCount >= delivery.BusinessWebhookEndpoint.MaxAttempts)
        {
            delivery.Status = BusinessWebhookDeliveryStatus.DeadLetter;
            delivery.DeadLetteredAt = completedAt;
            delivery.NextAttemptAt = null;
            delivery.ErrorMessage = Truncate(result.ErrorMessage, 2000);
        }
        else
        {
            delivery.Status = BusinessWebhookDeliveryStatus.Retry;
            delivery.ErrorMessage = Truncate(result.ErrorMessage, 2000);
            delivery.NextAttemptAt = completedAt.AddMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, delivery.AttemptCount - 1))));
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
