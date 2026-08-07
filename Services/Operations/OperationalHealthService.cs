using KorridorX.Configuration;
using Microsoft.Extensions.Options;
using KorridorX.Data;
using KorridorX.Dtos.Operations;
using KorridorX.Models.Enums;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Operations;

public sealed class OperationalHealthService : IOperationalHealthService
{
    private readonly AppDbContext _db;
    private readonly TreasuryOptions _treasuryOptions;

    public OperationalHealthService(AppDbContext db, IOptions<TreasuryOptions> treasuryOptions)
    {
        _db = db;
        _treasuryOptions = treasuryOptions.Value;
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
                new RiskHealthDto(0, 0, 0, 0),
                new ComplianceOperationsHealthDto(0, 0, 0, 0, 0),
                new SupportOperationsHealthDto(0, 0, 0, 0, 0),
                new TreasuryOperationsHealthDto(0, 0, 0, 0));
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
            await _db.Transfers.CountAsync(x => !x.IsDeleted && (x.IsComplianceHold || x.IsOperationalHold), ct),
            await _db.LoginHistories.CountAsync(x => !x.WasSuccessful && x.OccurredAt >= last24Hours, ct));

        var compliance = new ComplianceOperationsHealthDto(
            await _db.ComplianceCases.CountAsync(x =>
                !x.IsDeleted &&
                x.Status != ComplianceCaseStatus.Resolved &&
                x.Status != ComplianceCaseStatus.Closed,
                ct),
            await _db.ComplianceCases.CountAsync(x =>
                !x.IsDeleted &&
                x.IsBlocking &&
                x.Status != ComplianceCaseStatus.Closed &&
                (x.Status != ComplianceCaseStatus.Resolved ||
                 x.Decision == ComplianceCaseDecision.ConfirmedMatch ||
                 x.Decision == ComplianceCaseDecision.ReportFiled),
                ct),
            await _db.ComplianceCases.CountAsync(x =>
                !x.IsDeleted &&
                x.DueAt.HasValue &&
                x.DueAt.Value < now &&
                x.Status != ComplianceCaseStatus.Resolved &&
                x.Status != ComplianceCaseStatus.Closed,
                ct),
            await _db.ScreeningRecords.CountAsync(x =>
                !x.IsDeleted &&
                x.Status == ScreeningStatus.Failed &&
                x.ScreenedAt >= last24Hours,
                ct),
            await _db.ScreeningRecords.CountAsync(x =>
                !x.IsDeleted &&
                (x.Status == ScreeningStatus.PotentialMatch ||
                 x.Status == ScreeningStatus.ConfirmedMatch),
                ct));

        var support = new SupportOperationsHealthDto(
            await _db.SupportTickets.CountAsync(x =>
                !x.IsDeleted &&
                x.Status != SupportTicketStatus.Resolved &&
                x.Status != SupportTicketStatus.Closed,
                ct),
            await _db.SupportTickets.CountAsync(x =>
                !x.IsDeleted &&
                x.IsSlaBreached &&
                x.Status != SupportTicketStatus.Resolved &&
                x.Status != SupportTicketStatus.Closed,
                ct),
            await _db.TransferDisputes.CountAsync(x =>
                !x.IsDeleted &&
                x.Status != TransferDisputeStatus.Resolved &&
                x.Status != TransferDisputeStatus.Rejected &&
                x.Status != TransferDisputeStatus.Withdrawn,
                ct),
            await _db.TransferInvestigations.CountAsync(x =>
                !x.IsDeleted &&
                x.Status != TransferInvestigationStatus.Resolved &&
                x.Status != TransferInvestigationStatus.Closed,
                ct),
            await _db.TransferInvestigations.CountAsync(x =>
                !x.IsDeleted &&
                x.DueAt < now &&
                x.Status != TransferInvestigationStatus.Resolved &&
                x.Status != TransferInvestigationStatus.Closed,
                ct));

        var walletThresholds = await _db.LiquidityThresholds.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .ToListAsync(ct);
        var providerWallets = await _db.ProviderWalletBalances.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(ct);
        var providerWalletStaleBefore = now.AddMinutes(-_treasuryOptions.ProviderWalletStaleMinutes);
        var fxRateStaleBefore = now.AddMinutes(-_treasuryOptions.FxRateStaleMinutes);
        var lowLiquidityWallets = providerWallets.Count(wallet =>
        {
            var threshold = walletThresholds.FirstOrDefault(x =>
                string.Equals(x.ProviderCode, wallet.ProviderCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.CurrencyCode, wallet.CurrencyCode, StringComparison.OrdinalIgnoreCase));
            return threshold is not null && wallet.Balance < threshold.MinimumBalance;
        });

        var treasury = new TreasuryOperationsHealthDto(
            lowLiquidityWallets,
            providerWallets.Count(x => x.LastSyncedAt < providerWalletStaleBefore),
            await _db.ExchangeRates.CountAsync(x =>
                !x.IsDeleted && x.IsActive && x.EffectiveFrom < fxRateStaleBefore, ct),
            await _db.SettlementBatches.CountAsync(x =>
                !x.IsDeleted && x.Status == KorridorX.Models.Enums.SettlementBatchStatus.Variance, ct));

        var status = !connected
            ? "Critical"
            : notifications.DeadLetter > 0 ||
              provider.FailedWebhooks > 0 ||
              wallets.InconsistentWallets > 0 ||
              risk.BlockingFlags > 0 ||
              compliance.BlockingCases > 0 ||
              compliance.OverdueCases > 0 ||
              compliance.ScreeningFailuresLast24Hours > 0 ||
              support.SlaBreachedTickets > 0 ||
              support.OverdueInvestigations > 0 ||
              treasury.LowLiquidityWallets > 0 ||
              treasury.StaleProviderWallets > 0 ||
              treasury.SettlementVariances > 0
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
            risk,
            compliance,
            support,
            treasury);
    }

    private Task<int> CountNotificationsAsync(string status, CancellationToken ct) =>
        _db.NotificationMessages.CountAsync(x =>
            !x.IsDeleted && x.Status == status,
            ct);
}
