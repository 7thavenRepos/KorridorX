using KorridorX.Data;
using KorridorX.Dtos.Operations;
using KorridorX.Models.Enums;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Operations;

public sealed class OperationalHealthService : IOperationalHealthService
{
    private readonly AppDbContext _db;

    public OperationalHealthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OperationalHealthDto> GetAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var staleBefore = now.AddMinutes(-30);
        var connected = await _db.Database.CanConnectAsync(ct);

        if (!connected)
        {
            return new OperationalHealthDto(
                "Critical",
                false,
                now,
                new NotificationHealthDto(0, 0, 0, 0, 0),
                new ProviderHealthDto(0, 0, 0, 0),
                new WalletHealthDto(0, 0, 0),
                new PaymentHealthDto(0, 0, 0),
                new RiskHealthDto(0, 0, 0, 0));
        }

        var notifications = new NotificationHealthDto(
            await CountNotificationsAsync(NotificationStatuses.Pending, ct),
            await CountNotificationsAsync(NotificationStatuses.Retry, ct),
            await CountNotificationsAsync(NotificationStatuses.Processing, ct),
            await CountNotificationsAsync(NotificationStatuses.DeadLetter, ct),
            await _db.NotificationMessages.CountAsync(x =>
                !x.IsDeleted &&
                x.Status == NotificationStatuses.Sent &&
                x.SentAt.HasValue &&
                x.SentAt.Value >= last24Hours,
                ct));

        var provider = new ProviderHealthDto(
            await _db.ProviderRequestLogs.CountAsync(x =>
                !x.IsDeleted &&
                x.Status == ProviderRequestStatus.Failed &&
                x.RequestedAt >= last24Hours,
                ct),
            await _db.WebhookEvents.CountAsync(x =>
                !x.IsDeleted &&
                x.ProcessingStatus == WebhookProcessingStatus.Failed,
                ct),
            await _db.WebhookEvents.CountAsync(x =>
                !x.IsDeleted &&
                (x.ProcessingStatus == WebhookProcessingStatus.Pending ||
                 x.ProcessingStatus == WebhookProcessingStatus.Processing),
                ct),
            await _db.ProviderTransactions.CountAsync(x =>
                !x.IsDeleted &&
                (x.LastSyncedAt == null || x.LastSyncedAt < staleBefore),
                ct));

        var wallets = new WalletHealthDto(
            await _db.BusinessWallets.CountAsync(x =>
                !x.IsDeleted && x.Status == BusinessWalletStatus.Frozen,
                ct),
            await _db.BusinessWallets.CountAsync(x =>
                !x.IsDeleted &&
                (x.SettledBalance < 0 ||
                 x.AvailableBalance < 0 ||
                 x.HeldBalance < 0 ||
                 x.SettledBalance != x.AvailableBalance + x.HeldBalance),
                ct),
            await _db.BusinessWalletReservations.CountAsync(x =>
                !x.IsDeleted && x.Status == BusinessWalletReservationStatus.Active,
                ct));

        var payments = new PaymentHealthDto(
            await _db.Collections.CountAsync(x =>
                !x.IsDeleted &&
                x.Status == CollectionStatus.Failed &&
                x.FailedAt.HasValue &&
                x.FailedAt.Value >= last24Hours,
                ct),
            await _db.Payouts.CountAsync(x =>
                !x.IsDeleted &&
                x.Status == PayoutStatus.Failed &&
                x.FailedAt.HasValue &&
                x.FailedAt.Value >= last24Hours,
                ct),
            await _db.Collections.CountAsync(x =>
                !x.IsDeleted && x.Status == CollectionStatus.RefundPending,
                ct));

        var risk = new RiskHealthDto(
            await _db.AmlFlags.CountAsync(x => !x.IsDeleted && !x.IsResolved, ct),
            await _db.AmlFlags.CountAsync(x => !x.IsDeleted && !x.IsResolved && x.IsBlocking, ct),
            await _db.Transfers.CountAsync(x => !x.IsDeleted && x.IsComplianceHold, ct),
            await _db.LoginHistories.CountAsync(x => !x.WasSuccessful && x.OccurredAt >= last24Hours, ct));

        var status = !connected
            ? "Critical"
            : notifications.DeadLetter > 0 ||
              provider.FailedWebhooks > 0 ||
              wallets.InconsistentWallets > 0 ||
              risk.BlockingFlags > 0
                ? "Degraded"
                : "Healthy";

        return new OperationalHealthDto(
            status,
            connected,
            now,
            notifications,
            provider,
            wallets,
            payments,
            risk);
    }

    private Task<int> CountNotificationsAsync(string status, CancellationToken ct) =>
        _db.NotificationMessages.CountAsync(x =>
            !x.IsDeleted && x.Status == status,
            ct);
}
