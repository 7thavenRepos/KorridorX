using KorridorX.Data;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Transfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerTransferFundingService : IConsumerTransferFundingService
{
    private readonly AppDbContext _db;
    private readonly IConsumerWalletService _wallets;
    private readonly IFinancialReservationService _reservations;
    private readonly ITransferStatusService _transferStatus;

    public ConsumerTransferFundingService(
        AppDbContext db,
        IConsumerWalletService wallets,
        IFinancialReservationService reservations,
        ITransferStatusService transferStatus)
    {
        _db = db;
        _wallets = wallets;
        _reservations = reservations;
        _transferStatus = transferStatus;
    }

    public async Task<ConsumerTransferFundingDto> PrepareTransferAsync(
        Guid userId,
        Transfer transfer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (!transfer.CustomerProfileId.HasValue || transfer.BusinessProfileId.HasValue)
            throw new InvalidOperationException("Only consumer transfers can use consumer-wallet funding.");

        if (transfer.Status != TransferStatus.PendingPayment)
            throw new InvalidOperationException(
                $"Consumer-wallet funding cannot be prepared while the transfer status is '{transfer.Status}'.");

        var walletDto = await _wallets.CreateWalletAsync(
            userId,
            new CreateConsumerWalletRequestDto(transfer.SourceCurrencyCode),
            ct);

        var wallet = await _db.FinancialAccounts
            .FirstAsync(x => x.Id == walletDto.Id && !x.IsDeleted, ct);

        EnsureWalletForTransfer(wallet, userId, transfer.SourceCurrencyCode);
        transfer.SourceFinancialAccountId = wallet.Id;

        var reservation = await FindTransferReservationAsync(transfer.Id, ct);
        if (reservation is null)
        {
            var amountToReserve = Math.Min(wallet.AvailableBalance, transfer.TotalPayableAmount);
            if (amountToReserve > 0m)
            {
                reservation = await _reservations.ReserveAsync(
                    wallet.Id,
                    FinancialReservationType.Transfer,
                    nameof(Transfer),
                    transfer.Id,
                    amountToReserve,
                    userId,
                    ct: ct);
            }
        }

        ApplyFullyFundedTransitionIfReady(
            transfer,
            reservation,
            userId,
            "ConsumerWallet");

        return BuildFundingDto(transfer, wallet, reservation);
    }

    public async Task<ConsumerTransferFundingDto> GetFundingAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var transfer = await _db.Transfers
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .FirstOrDefaultAsync(x =>
                x.Id == transferId &&
                x.CustomerProfileId != null &&
                x.CustomerProfile!.UserId == userId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Transfer not found.");

        if (!transfer.SourceFinancialAccountId.HasValue)
            throw new InvalidOperationException("Transfer source wallet is not configured.");

        var wallet = await _db.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == transfer.SourceFinancialAccountId.Value &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Transfer source wallet was not found.");

        var reservation = await _db.FinancialReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Type == FinancialReservationType.Transfer &&
                x.RelatedEntityType == nameof(Transfer) &&
                x.RelatedEntityId == transfer.Id &&
                !x.IsDeleted,
                ct);

        return BuildFundingDto(transfer, wallet, reservation);
    }

    public async Task ApplySuccessfulCollectionAsync(
        Collection collection,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection.Purpose != PaymentOperationPurpose.Remittance ||
            !collection.TransferId.HasValue ||
            !collection.FinancialAccountId.HasValue)
        {
            return;
        }

        var transfer = collection.Transfer;
        if (transfer is null || transfer.CustomerProfileId is null || transfer.BusinessProfileId.HasValue)
            return;

        var customer = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transfer.CustomerProfileId.Value && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Consumer transfer customer profile was not found.");

        var wallet = await _db.FinancialAccounts
            .FirstOrDefaultAsync(x =>
                x.Id == collection.FinancialAccountId.Value &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == customer.UserId &&
                x.AssetCode == collection.CurrencyCode &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Consumer collection source wallet was not found.");

        if (transfer.SourceFinancialAccountId != wallet.Id)
            throw new InvalidOperationException("Collection wallet does not match the transfer source wallet.");

        var alreadyCredited = await _db.LedgerTransactions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                x.RelatedEntityType == nameof(Collection) &&
                x.RelatedEntityId == collection.Id &&
                !x.IsDeleted,
                ct);

        if (alreadyCredited)
            return;

        wallet.SettledBalance += collection.Amount;
        wallet.AvailableBalance += collection.Amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        PostLedger(
            wallet,
            LedgerTransactionType.ExternalCollectionCredit,
            collection.Amount,
            $"External collection {collection.Reference} credited to consumer wallet.",
            actionedByUserId,
            nameof(Collection),
            collection.Id,
            nameof(Transfer),
            transfer.Id,
            LedgerBalanceBucket.External,
            LedgerPostingSide.Debit,
            null,
            LedgerBalanceBucket.Available,
            LedgerPostingSide.Credit,
            wallet.AvailableBalance);

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            _db.TransferTimelineEvents.Add(new TransferTimelineEvent
            {
                TransferId = transfer.Id,
                EventType = "COLLECTION_CREDITED_TO_WALLET",
                Title = "Payment added to wallet",
                Description = $"The payment was credited to your {wallet.AssetCode} wallet because the transfer is no longer awaiting funding.",
                OccurredAt = DateTime.UtcNow
            });
            return;
        }

        var reservation = await FindTransferReservationAsync(transfer.Id, ct);
        var alreadyReserved = reservation?.Amount ?? 0m;
        var remainingNeeded = Math.Max(0m, transfer.TotalPayableAmount - alreadyReserved);
        var amountToReserve = Math.Min(wallet.AvailableBalance, remainingNeeded);

        if (amountToReserve > 0m)
        {
            if (reservation is null)
            {
                reservation = await _reservations.ReserveAsync(
                    wallet.Id,
                    FinancialReservationType.Transfer,
                    nameof(Transfer),
                    transfer.Id,
                    amountToReserve,
                    actionedByUserId,
                    ct: ct);
            }
            else
            {
                ExtendReservation(
                    wallet,
                    reservation,
                    amountToReserve,
                    actionedByUserId,
                    transfer.Id);
            }
        }

        ApplyFullyFundedTransitionIfReady(
            transfer,
            reservation,
            actionedByUserId,
            source);
    }

    public bool CaptureTransferReservation(
        Transfer transfer,
        Guid? actionedByUserId,
        string source)
    {
        if (!IsConsumerWalletTransfer(transfer))
            return false;

        var reservation = _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefault(x =>
                x.Type == FinancialReservationType.Transfer &&
                x.RelatedEntityType == nameof(Transfer) &&
                x.RelatedEntityId == transfer.Id &&
                !x.IsDeleted);

        if (reservation is null || reservation.Status == FinancialReservationStatus.Captured)
            return false;

        if (reservation.Status != FinancialReservationStatus.Active)
            return false;

        var remaining = reservation.RemainingAmount;
        if (remaining <= 0m)
            return false;

        var wallet = reservation.FinancialAccount;
        if (wallet.HeldBalance < remaining || wallet.SettledBalance < remaining)
            throw new InvalidOperationException("Consumer-wallet reservation balances are inconsistent.");

        wallet.HeldBalance -= remaining;
        wallet.SettledBalance -= remaining;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        reservation.CapturedAmount += remaining;
        reservation.Status = FinancialReservationStatus.Captured;
        reservation.CapturedAt = DateTime.UtcNow;
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedger(
            wallet,
            LedgerTransactionType.ReservationCapture,
            remaining,
            $"Captured consumer-wallet reservation after payout for transfer {transfer.Reference}. Source: {source}.",
            actionedByUserId,
            nameof(Transfer),
            transfer.Id,
            null,
            null,
            LedgerBalanceBucket.Held,
            LedgerPostingSide.Debit,
            wallet.HeldBalance,
            LedgerBalanceBucket.Settlement,
            LedgerPostingSide.Credit,
            null);

        return true;
    }

    public async Task<bool> ReleaseActiveTransferReservationAsync(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default)
    {
        if (!IsConsumerWalletTransfer(transfer))
            return false;

        var reservation = await FindTransferReservationAsync(transfer.Id, ct);
        if (reservation is null || reservation.Status == FinancialReservationStatus.Released)
            return false;

        if (reservation.Status != FinancialReservationStatus.Active)
            throw new InvalidOperationException(
                $"Only an active consumer-wallet reservation can be released during transfer cancellation. Current status: '{reservation.Status}'.");

        var remaining = reservation.RemainingAmount;
        if (remaining <= 0m)
            return false;

        await _reservations.ReleaseAsync(
            reservation.Id,
            remaining,
            $"{reason} Source: {source}.",
            actionedByUserId,
            ct);

        return true;
    }

    public bool ReleaseTransferReservation(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source)
    {
        if (!IsConsumerWalletTransfer(transfer))
            return false;

        var reservation = _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefault(x =>
                x.Type == FinancialReservationType.Transfer &&
                x.RelatedEntityType == nameof(Transfer) &&
                x.RelatedEntityId == transfer.Id &&
                !x.IsDeleted);

        if (reservation is null || reservation.Status == FinancialReservationStatus.Released)
            return false;

        var wallet = reservation.FinancialAccount;
        decimal amount;
        LedgerBalanceBucket debitBucket;
        decimal? debitBalanceAfter;

        if (reservation.Status == FinancialReservationStatus.Active)
        {
            amount = reservation.RemainingAmount;
            if (amount <= 0m)
                return false;
            if (wallet.HeldBalance < amount)
                throw new InvalidOperationException("Consumer-wallet held balance is inconsistent.");

            wallet.HeldBalance -= amount;
            wallet.AvailableBalance += amount;
            reservation.ReleasedAmount += amount;
            debitBucket = LedgerBalanceBucket.Held;
            debitBalanceAfter = wallet.HeldBalance;
        }
        else
        {
            amount = reservation.CapturedAmount;
            if (amount <= 0m)
                return false;

            wallet.SettledBalance += amount;
            wallet.AvailableBalance += amount;
            reservation.CapturedAmount = 0m;
            reservation.ReleasedAmount = reservation.Amount;
            debitBucket = LedgerBalanceBucket.Settlement;
            debitBalanceAfter = null;
        }

        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        reservation.Status = FinancialReservationStatus.Released;
        reservation.ReleasedAt = DateTime.UtcNow;
        reservation.ReleaseReason = Clean(reason, 1000);
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedger(
            wallet,
            LedgerTransactionType.ReservationRelease,
            amount,
            $"Released consumer-wallet funds for transfer {transfer.Reference}. Source: {source}. {reason}",
            actionedByUserId,
            nameof(Transfer),
            transfer.Id,
            null,
            null,
            debitBucket,
            LedgerPostingSide.Debit,
            debitBalanceAfter,
            LedgerBalanceBucket.Available,
            LedgerPostingSide.Credit,
            wallet.AvailableBalance);

        return true;
    }

    private async Task<FinancialReservation?> FindTransferReservationAsync(
        Guid transferId,
        CancellationToken ct) =>
        await _db.FinancialReservations
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.Type == FinancialReservationType.Transfer &&
                x.RelatedEntityType == nameof(Transfer) &&
                x.RelatedEntityId == transferId &&
                !x.IsDeleted,
                ct);

    private void ExtendReservation(
        FinancialAccount wallet,
        FinancialReservation reservation,
        decimal amount,
        Guid? actionedByUserId,
        Guid transferId)
    {
        if (reservation.Status != FinancialReservationStatus.Active)
            throw new InvalidOperationException("Only an active transfer reservation can be extended.");
        if (wallet.AvailableBalance < amount)
            throw new InvalidOperationException("Consumer wallet no longer has enough available balance.");

        wallet.AvailableBalance -= amount;
        wallet.HeldBalance += amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        reservation.Amount += amount;
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedger(
            wallet,
            LedgerTransactionType.TransferReservation,
            amount,
            $"Extended consumer-wallet reservation for transfer {transferId}.",
            actionedByUserId,
            nameof(Transfer),
            transferId,
            null,
            null,
            LedgerBalanceBucket.Available,
            LedgerPostingSide.Debit,
            wallet.AvailableBalance,
            LedgerBalanceBucket.Held,
            LedgerPostingSide.Credit,
            wallet.HeldBalance);
    }

    private void ApplyFullyFundedTransitionIfReady(
        Transfer transfer,
        FinancialReservation? reservation,
        Guid? actionedByUserId,
        string source)
    {
        if (transfer.Status != TransferStatus.PendingPayment)
            return;

        var reserved = reservation?.Amount ?? 0m;
        if (reserved < transfer.TotalPayableAmount)
            return;

        _transferStatus.ApplyTransition(
            transfer,
            TransferStatus.PaymentReceived,
            new TransferStatusTransitionContext(
                Source: source,
                Reason: "The full transfer amount has been reserved from the consumer wallet.",
                ChangedByUserId: actionedByUserId,
                EventType: "WALLET_FUNDING_COMPLETE",
                Title: "Transfer funded",
                Description: "Your wallet balance has been reserved and the transfer is ready for payout."));
    }

    private static ConsumerTransferFundingDto BuildFundingDto(
        Transfer transfer,
        FinancialAccount wallet,
        FinancialReservation? reservation)
    {
        var reserved = reservation?.Status == FinancialReservationStatus.Released
            ? 0m
            : Math.Max(0m, reservation?.Amount ?? 0m);

        var deficit = Math.Max(0m, transfer.TotalPayableAmount - reserved);

        return new ConsumerTransferFundingDto(
            transfer.Id,
            wallet.Id,
            wallet.AssetCode,
            transfer.TotalPayableAmount,
            reserved,
            deficit,
            reservation?.Status,
            deficit == 0m);
    }

    private static bool IsConsumerWalletTransfer(Transfer transfer) =>
        transfer.CustomerProfileId.HasValue &&
        !transfer.BusinessProfileId.HasValue &&
        transfer.SourceFinancialAccountId.HasValue;

    private static void EnsureWalletForTransfer(
        FinancialAccount wallet,
        Guid userId,
        string assetCode)
    {
        if (wallet.OwnerType != FinancialAccountOwnerType.User || wallet.OwnerId != userId)
            throw new InvalidOperationException("The source wallet does not belong to the consumer.");
        if (wallet.AccountType != FinancialAccountType.Customer)
            throw new InvalidOperationException("The source financial account is not a consumer wallet.");
        if (wallet.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("The source wallet is not active.");
        if (!string.Equals(wallet.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The source wallet asset does not match the transfer.");
    }

    private void PostLedger(
        FinancialAccount wallet,
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
            Reference = $"KXLED-{Guid.NewGuid():N}",
            AssetCode = wallet.AssetCode,
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
            FinancialAccountId = wallet.Id,
            FinancialAccount = wallet,
            BalanceBucket = debitBucket,
            Side = debitSide,
            Amount = amount,
            AccountBalanceAfter = debitBalanceAfter
        });

        transaction.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = wallet.Id,
            FinancialAccount = wallet,
            BalanceBucket = creditBucket,
            Side = creditSide,
            Amount = amount,
            AccountBalanceAfter = creditBalanceAfter
        });

        _db.LedgerTransactions.Add(transaction);
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }
}
