using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.FinancialCore;

public class FinancialReservationService : IFinancialReservationService
{
    private readonly AppDbContext _db;

    public FinancialReservationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FinancialReservation> ReserveAsync(
        Guid financialAccountId,
        FinancialReservationType type,
        string relatedEntityType,
        Guid relatedEntityId,
        decimal amount,
        Guid? actionedByUserId,
        string? contextEntityType = null,
        Guid? contextEntityId = null,
        CancellationToken ct = default)
    {
        if (amount <= 0m)
        {
            throw new InvalidOperationException("Reservation amount must be greater than zero.");
        }

        var existing = await _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.Type == type &&
                x.RelatedEntityType == relatedEntityType &&
                x.RelatedEntityId == relatedEntityId &&
                !x.IsDeleted,
                ct);

        if (existing is not null)
        {
            if (existing.Status == FinancialReservationStatus.Active)
            {
                return existing;
            }

            throw new InvalidOperationException("The reservation for this entity is already closed.");
        }

        var account = await _db.FinancialAccounts
            .FirstOrDefaultAsync(x => x.Id == financialAccountId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Financial account not found.");

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new InvalidOperationException("The financial account is not active.");
        }

        if (account.AvailableBalance < amount)
        {
            throw new InvalidOperationException(
                $"Insufficient {account.AssetCode} balance. Available: {account.AvailableBalance}; required: {amount}.");
        }

        account.AvailableBalance -= amount;
        account.HeldBalance += amount;
        account.LastUpdatedAt = DateTime.UtcNow;
        account.LastUpdatedByUserId = actionedByUserId;

        var reservation = new FinancialReservation
        {
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            Type = type,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            ContextEntityType = contextEntityType,
            ContextEntityId = contextEntityId,
            Reference = GenerateReference("KXRES"),
            Amount = amount,
            CapturedAmount = 0m,
            ReleasedAmount = 0m,
            Status = FinancialReservationStatus.Active,
            ReservedAt = DateTime.UtcNow,
            CreatedByUserId = actionedByUserId
        };

        _db.FinancialReservations.Add(reservation);
        var reservationLedgerType = type switch
        {
            FinancialReservationType.MarketplaceTrade => LedgerTransactionType.MarketplaceReservation,
            FinancialReservationType.InstantTrade => LedgerTransactionType.InstantReservation,
            _ => LedgerTransactionType.TransferReservation
        };

        PostReservationLedger(
            account,
            reservationLedgerType,
            amount,
            $"Reserved {amount} {account.AssetCode} for {relatedEntityType} {relatedEntityId}.",
            actionedByUserId,
            relatedEntityType,
            relatedEntityId,
            contextEntityType,
            contextEntityId,
            LedgerBalanceBucket.Available,
            LedgerPostingSide.Debit,
            account.AvailableBalance,
            LedgerBalanceBucket.Held,
            LedgerPostingSide.Credit,
            account.HeldBalance);

        return reservation;
    }

    public async Task<FinancialReservation> ReleaseAsync(
        Guid reservationId,
        decimal amount,
        string reason,
        Guid? actionedByUserId,
        CancellationToken ct = default)
    {
        var reservation = await _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x => x.Id == reservationId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Financial reservation not found.");

        var remaining = reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount;
        if (amount <= 0m)
        {
            throw new InvalidOperationException("Release amount must be greater than zero.");
        }
        if (amount > remaining)
        {
            throw new InvalidOperationException("Release amount exceeds the active reservation balance.");
        }

        if (reservation.Status != FinancialReservationStatus.Active)
        {
            throw new InvalidOperationException("Only an active reservation can release remaining funds.");
        }

        var account = reservation.FinancialAccount;
        if (account.HeldBalance < amount)
        {
            throw new InvalidOperationException("Financial-account held balance is inconsistent with the reservation.");
        }

        account.HeldBalance -= amount;
        account.AvailableBalance += amount;
        account.LastUpdatedAt = DateTime.UtcNow;
        account.LastUpdatedByUserId = actionedByUserId;

        reservation.ReleasedAmount += amount;
        if (reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount <= 0m)
        {
            reservation.Status = reservation.CapturedAmount > 0m
                ? FinancialReservationStatus.Captured
                : FinancialReservationStatus.Released;
            reservation.ReleasedAt = DateTime.UtcNow;

            if (reservation.CapturedAmount > 0m)
            {
                reservation.CapturedAt ??= DateTime.UtcNow;
            }
        }
        reservation.ReleaseReason = Clean(reason, 1000);
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostReservationLedger(
            account,
            LedgerTransactionType.ReservationRelease,
            amount,
            $"Released {amount} {account.AssetCode} from reservation {reservation.Reference}. {reason}",
            actionedByUserId,
            reservation.RelatedEntityType,
            reservation.RelatedEntityId,
            reservation.ContextEntityType,
            reservation.ContextEntityId,
            LedgerBalanceBucket.Held,
            LedgerPostingSide.Debit,
            account.HeldBalance,
            LedgerBalanceBucket.Available,
            LedgerPostingSide.Credit,
            account.AvailableBalance);

        return reservation;
    }

    private void PostReservationLedger(
        FinancialAccount account,
        LedgerTransactionType type,
        decimal amount,
        string description,
        Guid? userId,
        string relatedEntityType,
        Guid relatedEntityId,
        string? contextEntityType,
        Guid? contextEntityId,
        LedgerBalanceBucket debitBucket,
        LedgerPostingSide debitSide,
        decimal? debitBalanceAfter,
        LedgerBalanceBucket creditBucket,
        LedgerPostingSide creditSide,
        decimal? creditBalanceAfter)
    {
        var transaction = new LedgerTransaction
        {
            Reference = GenerateReference("KXLED"),
            AssetCode = account.AssetCode,
            Type = type,
            Status = LedgerTransactionStatus.Posted,
            Amount = amount,
            Description = Clean(description, 1000) ?? type.ToString(),
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            ContextEntityType = contextEntityType,
            ContextEntityId = contextEntityId,
            PostedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        transaction.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = transaction.Id,
            LedgerTransaction = transaction,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            BalanceBucket = debitBucket,
            Side = debitSide,
            Amount = amount,
            AccountBalanceAfter = debitBalanceAfter
        });

        transaction.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = transaction.Id,
            LedgerTransaction = transaction,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            BalanceBucket = creditBucket,
            Side = creditSide,
            Amount = amount,
            AccountBalanceAfter = creditBalanceAfter
        });

        _db.LedgerTransactions.Add(transaction);
    }

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
