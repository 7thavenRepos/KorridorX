using KorridorX.Data;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedPayoutSettlementService : IEmbeddedPayoutSettlementService
{
    private readonly AppDbContext _db;
    private readonly IFinancialReservationService _reservations;
    private readonly IEmbeddedWebhookPublisher _webhooks;

    public EmbeddedPayoutSettlementService(
        AppDbContext db,
        IFinancialReservationService reservations,
        IEmbeddedWebhookPublisher webhooks)
    {
        _db = db;
        _reservations = reservations;
        _webhooks = webhooks;
    }

    public async Task ApplyStatusEffectAsync(
        Payout payout,
        bool statusChanged,
        CancellationToken ct = default)
    {
        if (payout.Purpose != PaymentOperationPurpose.Withdrawal || !statusChanged)
            return;

        var reservation = await _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.Type == FinancialReservationType.Withdrawal &&
                x.RelatedEntityType == nameof(Payout) &&
                x.RelatedEntityId == payout.Id &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Embedded payout reservation was not found.");

        var remaining = reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount;

        if (payout.Status == PayoutStatus.Successful && remaining > 0m)
        {
            await _reservations.CaptureAsync(reservation.Id, remaining, null, ct);
        }
        else if (payout.Status == PayoutStatus.Failed && remaining > 0m)
        {
            await _reservations.ReleaseAsync(
                reservation.Id,
                remaining,
                payout.FailureReason ?? "Embedded payout failed.",
                null,
                ct);
        }
        else if (payout.Status == PayoutStatus.Reversed)
        {
            if (remaining > 0m)
            {
                await _reservations.ReleaseAsync(
                    reservation.Id,
                    remaining,
                    "Embedded payout was reversed before settlement.",
                    null,
                    ct);
            }
            else if (reservation.CapturedAmount > 0m)
            {
                await CreditReversalAsync(payout, reservation, ct);
            }
        }

        var businessCustomerId = payout.ContextEntityId
            ?? throw new InvalidOperationException("Embedded payout is missing BusinessCustomer context.");

        var businessProfileId = await _db.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.Id == businessCustomerId && !x.IsDeleted)
            .Select(x => x.BusinessProfileId)
            .SingleAsync(ct);

        var eventType = payout.Status switch
        {
            PayoutStatus.Successful => "payout.completed",
            PayoutStatus.Failed => "payout.failed",
            PayoutStatus.Reversed => "payout.reversed",
            PayoutStatus.Initiated or PayoutStatus.Processing => "payout.processing",
            _ => "payout.updated"
        };

        await _webhooks.PublishAsync(
            businessProfileId,
            eventType,
            new
            {
                id = payout.Id,
                businessCustomerId,
                financialAccountId = payout.FinancialAccountId,
                externalReference = payout.Reference,
                amount = payout.Amount,
                currencyCode = payout.CurrencyCode,
                status = payout.Status.ToString(),
                provider = payout.ProviderCode,
                providerPayoutId = payout.ProviderPayoutId,
                providerReference = payout.ProviderReference,
                failureReason = payout.FailureReason,
                initiatedAt = payout.InitiatedAt,
                completedAt = payout.CompletedAt,
                failedAt = payout.FailedAt,
                reversedAt = payout.ReversedAt
            },
            ct);
    }

    private async Task CreditReversalAsync(
        Payout payout,
        FinancialReservation reservation,
        CancellationToken ct)
    {
        var existing = await _db.LedgerTransactions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Type == LedgerTransactionType.Refund &&
                x.RelatedEntityType == nameof(Payout) &&
                x.RelatedEntityId == payout.Id &&
                !x.IsDeleted,
                ct);

        if (existing)
            return;

        var account = reservation.FinancialAccount;
        var amount = reservation.CapturedAmount;

        account.SettledBalance += amount;
        account.AvailableBalance += amount;
        account.LastUpdatedAt = DateTime.UtcNow;

        var ledger = new LedgerTransaction
        {
            Reference = BuildReference("KXREV"),
            AssetCode = account.AssetCode,
            Type = LedgerTransactionType.Refund,
            Status = LedgerTransactionStatus.Posted,
            Amount = amount,
            Description = $"Recredited reversed embedded payout {payout.Reference}.",
            RelatedEntityType = nameof(Payout),
            RelatedEntityId = payout.Id,
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = payout.ContextEntityId,
            PostedAt = DateTime.UtcNow
        };

        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccount = account,
            FinancialAccountId = account.Id,
            BalanceBucket = LedgerBalanceBucket.External,
            Side = LedgerPostingSide.Debit,
            Amount = amount
        });

        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccount = account,
            FinancialAccountId = account.Id,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = amount,
            AccountBalanceAfter = account.AvailableBalance
        });

        _db.LedgerTransactions.Add(ledger);
    }

    private static string BuildReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }
}
