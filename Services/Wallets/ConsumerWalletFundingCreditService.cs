using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletFundingCreditService : IConsumerWalletFundingCreditService
{
    private readonly AppDbContext _db;

    public ConsumerWalletFundingCreditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task ApplySuccessfulCollectionAsync(
        Collection collection,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection.Purpose != PaymentOperationPurpose.AccountFunding ||
            collection.TransferId.HasValue ||
            !collection.FinancialAccountId.HasValue)
        {
            return;
        }

        var wallet = await _db.FinancialAccounts
            .FirstOrDefaultAsync(x =>
                x.Id == collection.FinancialAccountId.Value &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.AccountType == FinancialAccountType.Customer &&
                x.AssetCode == collection.CurrencyCode &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Consumer wallet funding account was not found.");

        var alreadyCredited = await _db.LedgerTransactions
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

        _db.LedgerTransactions.Add(transaction);
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
