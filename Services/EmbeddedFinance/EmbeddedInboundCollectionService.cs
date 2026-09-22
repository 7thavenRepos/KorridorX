using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedInboundCollectionService : IEmbeddedInboundCollectionService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedWebhookPublisher _webhooks;

    public EmbeddedInboundCollectionService(AppDbContext db, IEmbeddedWebhookPublisher webhooks)
    {
        _db = db;
        _webhooks = webhooks;
    }

    public async Task<bool> TryProcessBlaaizDepositAsync(
        EmbeddedInboundCollectionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId)) return false;

        var providerStatus = request.ProviderStatus.Trim().ToUpperInvariant();
        if (providerStatus is not ("SUCCESSFUL" or "COMPLETED")) return false;
        if (request.Amount <= 0) throw new InvalidOperationException("Embedded collection amount must be greater than zero.");

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency)) throw new InvalidOperationException("Embedded collection currency is required.");

        var mapping = await ResolveMappingAsync(request, ct);
        if (mapping is null) return false;
        if (mapping.Status != ProviderAccountMappingStatus.Active) throw new InvalidOperationException("The matched provider collection account is not active.");

        var collectionAccount = mapping.CollectionAccount;
        if (collectionAccount.BusinessCustomer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("The matched business customer is not active.");
        if (!string.IsNullOrWhiteSpace(request.ProviderCustomerId) && mapping.ProviderCustomerId != request.ProviderCustomerId.Trim())
            throw new InvalidOperationException("Provider deposit customer does not match the collection account.");
        if (!string.IsNullOrWhiteSpace(request.AccountNumber) && mapping.AccountNumber is not null && mapping.AccountNumber != request.AccountNumber.Trim())
            throw new InvalidOperationException("Provider deposit account number does not match the collection account.");
        if (!string.IsNullOrWhiteSpace(request.ProviderAccountReference) && mapping.ProviderReference is not null && mapping.ProviderReference != request.ProviderAccountReference.Trim())
            throw new InvalidOperationException("Provider deposit account reference does not match the collection account.");
        if (collectionAccount.Status != CollectionAccountStatus.Active) throw new InvalidOperationException("The matched collection account is not active.");
        if (!string.Equals(collectionAccount.AssetCode, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Provider deposit currency mismatch. Collection account expects {collectionAccount.AssetCode}, received {currency}.");

        var existingProviderTransaction = await _db.ProviderTransactions.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == request.ProviderTransactionId, ct);
        if (existingProviderTransaction?.CollectionId is not null)
        {
            await ValidateCompletedReplayAsync(existingProviderTransaction.CollectionId.Value, request, currency, ct);
            return true;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var providerTransaction = await _db.ProviderTransactions.FirstOrDefaultAsync(x =>
            x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == request.ProviderTransactionId, ct);
        if (providerTransaction?.CollectionId is not null)
        {
            await ValidateCompletedReplayAsync(providerTransaction.CollectionId.Value, request, currency, ct);
            await tx.CommitAsync(ct);
            return true;
        }

        var financialAccount = await _db.FinancialAccounts.FirstOrDefaultAsync(x =>
            x.Id == collectionAccount.FinancialAccountId &&
            x.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
            x.OwnerId == collectionAccount.BusinessCustomerId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Mapped customer Financial Account was not found.");

        if (financialAccount.Status != FinancialAccountStatus.Active) throw new InvalidOperationException("Mapped customer Financial Account is not active.");
        if (!string.Equals(financialAccount.AssetCode, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mapped Financial Account asset does not match the provider deposit currency.");

        var idempotencyScope = $"EmbeddedCollection:{ProviderCode.Blaaiz}:{collectionAccount.Id}";
        var idempotencyKey = request.ProviderTransactionId.Trim();
        var requestHash = Hash(new
        {
            Provider = ProviderCode.Blaaiz.ToString(),
            request.ProviderTransactionId,
            CurrencyCode = currency,
            request.Amount,
            CollectionAccountId = collectionAccount.Id,
            FinancialAccountId = financialAccount.Id
        });

        var existingLedger = await _db.LedgerTransactions.AsNoTracking().FirstOrDefaultAsync(x =>
            x.IdempotencyScope == idempotencyScope && x.IdempotencyKey == idempotencyKey, ct);
        if (existingLedger is not null)
        {
            if (!string.Equals(existingLedger.IdempotencyRequestHash, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Provider transaction idempotency key was reused with different deposit details.");
            await tx.CommitAsync(ct);
            return true;
        }

        var now = request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt.ToUniversalTime();
        var collection = new Collection
        {
            Purpose = PaymentOperationPurpose.AccountFunding,
            FinancialAccountId = financialAccount.Id,
            FinancialAccount = financialAccount,
            RelatedEntityType = nameof(CollectionAccount),
            RelatedEntityId = collectionAccount.Id,
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = collectionAccount.BusinessCustomerId,
            Reference = $"COL-EMB-{Guid.NewGuid():N}"[..40],
            CurrencyCode = currency,
            Amount = request.Amount,
            PaymentMethod = PaymentMethod.VirtualAccount,
            Status = CollectionStatus.Successful,
            ProviderCode = ProviderCode.Blaaiz.ToString(),
            ProviderCollectionId = request.ProviderTransactionId.Trim(),
            ProviderReference = Clean(request.ProviderReference, 150) ?? Clean(request.ProviderAccountReference, 150),
            VirtualAccountNumber = Clean(mapping.AccountNumber ?? request.AccountNumber, 100),
            VirtualAccountName = Clean(mapping.AccountName, 200),
            VirtualAccountBankName = Clean(mapping.BankName, 150),
            InitiatedAt = now,
            ConfirmedAt = now
        };
        _db.Collections.Add(collection);

        financialAccount.SettledBalance += request.Amount;
        financialAccount.AvailableBalance += request.Amount;
        financialAccount.LastUpdatedAt = now;

        var ledger = new LedgerTransaction
        {
            Reference = $"LED-EMB-{Guid.NewGuid():N}"[..40],
            AssetCode = currency,
            Type = LedgerTransactionType.ExternalCollectionCredit,
            Status = LedgerTransactionStatus.Posted,
            Amount = request.Amount,
            Description = $"Blaaiz deposit credited to embedded collection account {collectionAccount.ExternalReference}.",
            IdempotencyScope = idempotencyScope,
            IdempotencyKey = idempotencyKey,
            IdempotencyRequestHash = requestHash,
            RelatedEntityType = nameof(Collection),
            RelatedEntityId = collection.Id,
            ContextEntityType = nameof(CollectionAccount),
            ContextEntityId = collectionAccount.Id,
            PostedAt = now
        };
        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = financialAccount.Id,
            FinancialAccount = financialAccount,
            BalanceBucket = LedgerBalanceBucket.External,
            Side = LedgerPostingSide.Debit,
            Amount = request.Amount
        });
        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = financialAccount.Id,
            FinancialAccount = financialAccount,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = request.Amount,
            AccountBalanceAfter = financialAccount.AvailableBalance
        });
        _db.LedgerTransactions.Add(ledger);

        providerTransaction ??= new ProviderTransaction
        {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderTransactionId = request.ProviderTransactionId.Trim()
        };
        if (_db.Entry(providerTransaction).State == EntityState.Detached) _db.ProviderTransactions.Add(providerTransaction);

        providerTransaction.CollectionId = collection.Id;
        providerTransaction.Collection = collection;
        providerTransaction.ProviderReference = Clean(request.ProviderReference, 150) ?? Clean(request.ProviderAccountReference, 150);
        providerTransaction.TransactionType = "collection";
        providerTransaction.ProviderStatus = providerStatus;
        providerTransaction.CurrencyCode = currency;
        providerTransaction.Amount = request.Amount;
        providerTransaction.AmountWithoutFee = request.AmountWithoutFee;
        providerTransaction.ProviderFeeAmount = request.ProviderFeeAmount;
        providerTransaction.ProviderFeeCurrencyCode = Clean(request.ProviderFeeCurrencyCode, 10) ?? currency;
        providerTransaction.RawPayloadJson = request.RawPayloadJson;
        providerTransaction.ProviderCreatedAt = now;
        providerTransaction.LastSyncedAt = DateTime.UtcNow;
        providerTransaction.LastUpdatedAt = DateTime.UtcNow;

        await _webhooks.PublishAsync(collectionAccount.BusinessProfileId, "collection.completed", new
        {
            id = collection.Id,
            collection.Reference,
            businessCustomerId = collectionAccount.BusinessCustomerId,
            collectionAccountId = collectionAccount.Id,
            collectionAccountExternalReference = collectionAccount.ExternalReference,
            financialAccountId = financialAccount.Id,
            provider = ProviderCode.Blaaiz.ToString(),
            providerTransactionId = request.ProviderTransactionId,
            providerReference = providerTransaction.ProviderReference,
            amount = collection.Amount,
            currencyCode = collection.CurrencyCode,
            status = collection.Status.ToString(),
            confirmedAt = collection.ConfirmedAt
        }, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task ValidateCompletedReplayAsync(
        Guid collectionId,
        EmbeddedInboundCollectionRequest request,
        string normalizedCurrency,
        CancellationToken ct)
    {
        var collection = await _db.Collections.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == collectionId && !x.IsDeleted,
            ct)
            ?? throw new InvalidOperationException(
                "Existing provider transaction points to a missing collection.");

        if (collection.Amount != request.Amount ||
            !string.Equals(
                collection.CurrencyCode,
                normalizedCurrency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Provider transaction idempotency key was reused with different deposit details.");
        }
    }

    private async Task<ProviderAccountMapping?> ResolveMappingAsync(EmbeddedInboundCollectionRequest request, CancellationToken ct)
    {
        var providerCode = ProviderCode.Blaaiz.ToString();
        var query = _db.ProviderAccountMappings
            .Include(x => x.CollectionAccount)
            .ThenInclude(x => x.BusinessCustomer)
            .Where(x => x.ProviderCode == providerCode && !x.IsDeleted && !x.CollectionAccount.IsDeleted && !x.CollectionAccount.BusinessCustomer.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.ProviderAccountId))
        {
            var value = request.ProviderAccountId.Trim();
            // An explicit unknown account must never fall back to a customer match.
            return await query.SingleOrDefaultAsync(x => x.ProviderAccountId == value, ct);
        }
        if (!string.IsNullOrWhiteSpace(request.ProviderAccountReference))
        {
            var value = request.ProviderAccountReference.Trim();
            return Unique(await query.Where(x => x.ProviderReference == value).Take(2).ToListAsync(ct));
        }
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        query = query.Where(x => x.CollectionAccount.AssetCode == currency);
        if (!string.IsNullOrWhiteSpace(request.ProviderCustomerId))
        {
            var customer = request.ProviderCustomerId.Trim();
            query = query.Where(x => x.ProviderCustomerId == customer);
        }
        if (!string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            var number = request.AccountNumber.Trim();
            return Unique(await query.Where(x => x.AccountNumber == number).Take(2).ToListAsync(ct));
        }
        if (!string.IsNullOrWhiteSpace(request.ProviderCustomerId))
            return Unique(await query.Take(2).ToListAsync(ct));
        return null;
    }

    private static ProviderAccountMapping? Unique(List<ProviderAccountMapping> candidates) => candidates.Count switch
    {
        0 => null,
        1 => candidates[0],
        _ => throw new InvalidOperationException("Blaaiz deposit matched multiple collection accounts; unambiguous account identification is required.")
    };

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
