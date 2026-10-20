using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletFundingCreditService : IConsumerWalletFundingCreditService
{
    private readonly AppDbContext _db;
    private readonly INotificationQueueService _notifications;

    public ConsumerWalletFundingCreditService(AppDbContext db, INotificationQueueService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task ApplySuccessfulCollectionAsync(
        Collection collection,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection.Status != CollectionStatus.Successful ||
            collection.Purpose != PaymentOperationPurpose.AccountFunding ||
            collection.TransferId.HasValue ||
            !collection.FinancialAccountId.HasValue)
        {
            return;
        }

        var wallet = await _db.FinancialAccounts
            .FirstOrDefaultAsync(x =>
                x.Id == collection.FinancialAccountId.Value &&
                (x.OwnerType == FinancialAccountOwnerType.User || x.OwnerType == FinancialAccountOwnerType.Business) &&
                x.AccountType == FinancialAccountType.Customer &&
                x.AssetCode == collection.CurrencyCode &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Consumer wallet funding account was not found.");

        var alreadyCredited = _db.LedgerTransactions.Local.Any(x =>
                x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                x.RelatedEntityType == nameof(Collection) && x.RelatedEntityId == collection.Id && !x.IsDeleted) ||
            await _db.LedgerTransactions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                x.RelatedEntityType == nameof(Collection) &&
                x.RelatedEntityId == collection.Id &&
                !x.IsDeleted,
                ct);

        if (alreadyCredited)
        {
            return;
        }

        wallet.SettledBalance += collection.Amount;
        wallet.AvailableBalance += collection.Amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        var transaction = new LedgerTransaction
        {
            Reference = $"KXLED-{Guid.NewGuid():N}",
            AssetCode = wallet.AssetCode,
            Type = LedgerTransactionType.ExternalCollectionCredit,
            Status = LedgerTransactionStatus.Posted,
            Amount = collection.Amount,
            Description = $"Wallet top-up {collection.Reference} credited. Source: {Clean(source, 100)}.",
            RelatedEntityType = nameof(Collection),
            RelatedEntityId = collection.Id,
            ContextEntityType = nameof(FinancialAccount),
            ContextEntityId = wallet.Id,
            PostedAt = DateTime.UtcNow,
            CreatedByUserId = actionedByUserId
        };

        transaction.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = wallet.Id,
            FinancialAccount = wallet,
            BalanceBucket = LedgerBalanceBucket.External,
            Side = LedgerPostingSide.Debit,
            Amount = collection.Amount
        });

        transaction.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = wallet.Id,
            FinancialAccount = wallet,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = collection.Amount,
            AccountBalanceAfter = wallet.AvailableBalance
        });

        KorridorX.Services.PaymentChannels.ProviderCollectionCreditGuard.ApplyLedgerIdentity(collection, transaction);
        _db.LedgerTransactions.Add(transaction);

        // Queue durable delivery in the same SaveChanges/settlement transaction as
        // the credit. Delivery is performed later; SMTP/FCM outages cannot undo it.
        // The existing credit guard and ledger replay check also guard this notice.
        var notice = WalletActivityNotificationFactory.FundingReceived(wallet.AssetCode, collection.Amount, collection.Reference);
        if (wallet.OwnerType == FinancialAccountOwnerType.Business)
            await _notifications.QueueBusinessAsync(wallet.OwnerId, notice.Subject, notice.Body,
                WalletActivityNotificationFactory.FundingEntityType, collection.Id, ct);
        else
            await _notifications.QueueUserAsync(wallet.OwnerId, notice.Subject, notice.Body,
                WalletActivityNotificationFactory.FundingEntityType, collection.Id, ct);
    }

    private static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Provider";
        }

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }
}
