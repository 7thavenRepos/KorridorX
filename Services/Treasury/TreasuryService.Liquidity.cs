using System.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Treasury;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Treasury;

public sealed partial class TreasuryService
{
    private static readonly FinancialAccountType[] LiquidityAccountTypes =
    {
        FinancialAccountType.House,
        FinancialAccountType.Treasury,
        FinancialAccountType.ProviderClearing,
        FinancialAccountType.Settlement
    };

    public async Task<IReadOnlyList<TreasuryLiquidityPositionDto>> GetLiquidityPositionsAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-_options.ProviderWalletStaleMinutes);

        var thresholds = await _db.LiquidityThresholds.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var accounts = await _db.FinancialAccounts.AsNoTracking()
            .Where(x => !x.IsDeleted && LiquidityAccountTypes.Contains(x.AccountType))
            .OrderBy(x => x.AssetCode)
            .ThenBy(x => x.AccountType)
            .ThenBy(x => x.AccountCode)
            .ToListAsync(ct);

        var providerWallets = await _db.ProviderWalletBalances.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CurrencyCode)
            .ThenBy(x => x.ProviderCode)
            .ToListAsync(ct);

        var inFlight = await _db.DigitalAssetNetworkTransactions.AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == DigitalAssetTransactionStatus.Observed ||
                 x.Status == DigitalAssetTransactionStatus.Confirming))
            .Select(x => new
            {
                x.AssetCode,
                x.AssetNetwork.NetworkCode,
                x.Direction,
                x.Amount
            })
            .ToListAsync(ct);

        var inFlightMap = inFlight
            .GroupBy(x => NetworkKey(x.AssetCode, x.NetworkCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (
                    Inbound: g.Where(x => x.Direction == DigitalAssetTransactionDirection.Inbound).Sum(x => x.Amount),
                    Outbound: g.Where(x => x.Direction == DigitalAssetTransactionDirection.Outbound).Sum(x => x.Amount)),
                StringComparer.OrdinalIgnoreCase);

        var result = new List<TreasuryLiquidityPositionDto>();

        foreach (var account in accounts)
        {
            var threshold = thresholds.FirstOrDefault(x =>
                x.ScopeType == TreasuryLiquidityScopeType.FinancialAccount &&
                x.CurrencyCode == account.AssetCode &&
                x.FinancialAccountType == account.AccountType);

            result.Add(new TreasuryLiquidityPositionDto
            {
                ScopeType = TreasuryLiquidityScopeType.FinancialAccount,
                SourceId = account.Id,
                SourceName = account.AccountCode,
                AssetCode = account.AssetCode,
                FinancialAccountType = account.AccountType,
                FinancialAccountStatus = account.Status,
                SettledBalance = account.SettledBalance,
                AvailableBalance = account.AvailableBalance,
                HeldBalance = account.HeldBalance,
                MinimumBalance = threshold?.MinimumBalance,
                TargetBalance = threshold?.TargetBalance,
                MaximumBalance = threshold?.MaximumBalance,
                LiquidityStatus = ResolveUnifiedLiquidityStatus(
                    account.AvailableBalance,
                    account.Status == FinancialAccountStatus.Active,
                    threshold)
            });
        }

        foreach (var wallet in providerWallets)
        {
            var threshold = thresholds.FirstOrDefault(x =>
                x.ScopeType == TreasuryLiquidityScopeType.ProviderWallet &&
                x.ProviderCode == wallet.ProviderCode &&
                x.CurrencyCode == wallet.CurrencyCode &&
                (x.NetworkCode ?? "") == (wallet.NetworkCode ?? ""));

            var flight = inFlightMap.GetValueOrDefault(
                NetworkKey(wallet.CurrencyCode, wallet.NetworkCode));

            result.Add(new TreasuryLiquidityPositionDto
            {
                ScopeType = TreasuryLiquidityScopeType.ProviderWallet,
                SourceId = wallet.Id,
                SourceName = wallet.ProviderWalletId,
                AssetCode = wallet.CurrencyCode,
                ProviderCode = wallet.ProviderCode,
                NetworkCode = wallet.NetworkCode,
                ExternalBalance = wallet.Balance,
                PendingNetworkInbound = flight.Inbound,
                PendingNetworkOutbound = flight.Outbound,
                MinimumBalance = threshold?.MinimumBalance,
                TargetBalance = threshold?.TargetBalance,
                MaximumBalance = threshold?.MaximumBalance,
                LiquidityStatus = ResolveUnifiedLiquidityStatus(
                    wallet.Balance,
                    wallet.IsActive,
                    threshold),
                LastSyncedAt = wallet.LastSyncedAt,
                IsStale = wallet.LastSyncedAt < staleBefore
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<TreasuryAssetExposureDto>> GetAssetExposureAsync(
        CancellationToken ct = default)
    {
        var positions = await GetLiquidityPositionsAsync(ct);

        return positions
            .GroupBy(x => x.AssetCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TreasuryAssetExposureDto
            {
                AssetCode = g.Key,
                HouseAvailable = g.Where(x => x.FinancialAccountType == FinancialAccountType.House).Sum(x => x.AvailableBalance),
                TreasuryAvailable = g.Where(x => x.FinancialAccountType == FinancialAccountType.Treasury).Sum(x => x.AvailableBalance),
                ProviderClearingAvailable = g.Where(x => x.FinancialAccountType == FinancialAccountType.ProviderClearing).Sum(x => x.AvailableBalance),
                SettlementAvailable = g.Where(x => x.FinancialAccountType == FinancialAccountType.Settlement).Sum(x => x.AvailableBalance),
                ExternalProviderBalance = g.Sum(x => x.ExternalBalance),
                PendingNetworkInbound = g.Sum(x => x.PendingNetworkInbound),
                PendingNetworkOutbound = g.Sum(x => x.PendingNetworkOutbound),
                ObservedLiquidity =
                    g.Sum(x => x.AvailableBalance) +
                    g.Sum(x => x.ExternalBalance) +
                    g.Sum(x => x.PendingNetworkInbound) -
                    g.Sum(x => x.PendingNetworkOutbound)
            })
            .OrderBy(x => x.AssetCode)
            .ToList();
    }

    public async Task<IReadOnlyList<TreasuryLiquidityAlertDto>> GetLiquidityAlertsAsync(
        CancellationToken ct = default)
    {
        var positions = await GetLiquidityPositionsAsync(ct);

        return positions
            .Where(x =>
                x.LiquidityStatus is LiquidityPositionStatus.Critical or LiquidityPositionStatus.Low ||
                x.IsStale)
            .Select(x => new TreasuryLiquidityAlertDto
            {
                Severity = x.LiquidityStatus == LiquidityPositionStatus.Critical
                    ? TreasuryLiquidityAlertSeverity.Critical
                    : TreasuryLiquidityAlertSeverity.Warning,
                ScopeType = x.ScopeType,
                SourceId = x.SourceId,
                AssetCode = x.AssetCode,
                ProviderCode = x.ProviderCode,
                NetworkCode = x.NetworkCode,
                LiquidityStatus = x.LiquidityStatus,
                Message = x.IsStale
                    ? $"{x.SourceName} {x.AssetCode} liquidity snapshot is stale."
                    : $"{x.SourceName} {x.AssetCode} liquidity is {x.LiquidityStatus.ToString().ToLowerInvariant()}."
            })
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.AssetCode)
            .ToList();
    }

    public async Task<IReadOnlyList<TreasuryUnifiedRebalanceSuggestionDto>>
        GetUnifiedRebalanceSuggestionsAsync(CancellationToken ct = default)
    {
        var positions = await GetLiquidityPositionsAsync(ct);
        var suggestions = new List<TreasuryUnifiedRebalanceSuggestionDto>();

        foreach (var house in positions.Where(x =>
                     x.ScopeType == TreasuryLiquidityScopeType.FinancialAccount &&
                     x.FinancialAccountType == FinancialAccountType.House &&
                     x.TargetBalance.HasValue &&
                     x.AvailableBalance < x.TargetBalance.Value))
        {
            var treasury = positions
                .Where(x =>
                    x.ScopeType == TreasuryLiquidityScopeType.FinancialAccount &&
                    x.FinancialAccountType == FinancialAccountType.Treasury &&
                    x.AssetCode.Equals(house.AssetCode, StringComparison.OrdinalIgnoreCase) &&
                    x.TargetBalance.HasValue &&
                    x.AvailableBalance > x.TargetBalance.Value)
                .OrderByDescending(x => x.AvailableBalance - x.TargetBalance!.Value)
                .FirstOrDefault();

            if (treasury is null) continue;

            var amount = Math.Min(
                house.TargetBalance!.Value - house.AvailableBalance,
                treasury.AvailableBalance - treasury.TargetBalance!.Value);

            if (amount <= 0m) continue;

            suggestions.Add(new TreasuryUnifiedRebalanceSuggestionDto
            {
                ActionType = TreasuryLiquidityActionType.InternalTransfer,
                AssetCode = house.AssetCode,
                FromFinancialAccountId = treasury.SourceId,
                ToFinancialAccountId = house.SourceId,
                SuggestedAmount = amount,
                Reason = "House liquidity is below target while Treasury liquidity is above target."
            });
        }

        foreach (var provider in positions.Where(x =>
                     x.ScopeType == TreasuryLiquidityScopeType.ProviderWallet &&
                     x.TargetBalance.HasValue))
        {
            var treasury = positions
                .Where(x =>
                    x.ScopeType == TreasuryLiquidityScopeType.FinancialAccount &&
                    x.FinancialAccountType == FinancialAccountType.Treasury &&
                    x.AssetCode.Equals(provider.AssetCode, StringComparison.OrdinalIgnoreCase) &&
                    x.TargetBalance.HasValue)
                .OrderByDescending(x => x.AvailableBalance)
                .FirstOrDefault();

            if (provider.ExternalBalance < provider.TargetBalance.Value &&
                treasury is not null &&
                treasury.AvailableBalance > treasury.TargetBalance!.Value)
            {
                var amount = Math.Min(
                    provider.TargetBalance.Value - provider.ExternalBalance,
                    treasury.AvailableBalance - treasury.TargetBalance.Value);

                if (amount > 0m)
                {
                    suggestions.Add(new TreasuryUnifiedRebalanceSuggestionDto
                    {
                        ActionType = TreasuryLiquidityActionType.ProviderTopUp,
                        AssetCode = provider.AssetCode,
                        FromFinancialAccountId = treasury.SourceId,
                        ProviderWalletBalanceId = provider.SourceId,
                        ProviderCode = provider.ProviderCode,
                        NetworkCode = provider.NetworkCode,
                        SuggestedAmount = amount,
                        Reason = "Provider liquidity is below target and internal Treasury liquidity is above target."
                    });
                }
            }

            if (provider.MaximumBalance.HasValue &&
                provider.ExternalBalance > provider.MaximumBalance.Value)
            {
                suggestions.Add(new TreasuryUnifiedRebalanceSuggestionDto
                {
                    ActionType = TreasuryLiquidityActionType.ProviderSweep,
                    AssetCode = provider.AssetCode,
                    ProviderWalletBalanceId = provider.SourceId,
                    ProviderCode = provider.ProviderCode,
                    NetworkCode = provider.NetworkCode,
                    SuggestedAmount = provider.ExternalBalance - provider.TargetBalance.Value,
                    Reason = "Provider liquidity is above maximum and should be swept toward Treasury."
                });
            }
        }

        return suggestions.OrderBy(x => x.AssetCode).ThenBy(x => x.ActionType).ToList();
    }

    public async Task<InternalLiquidityTransferResultDto> ExecuteInternalLiquidityTransferAsync(
        Guid userId,
        ExecuteInternalLiquidityTransferRequestDto request,
        CancellationToken ct = default)
    {
        if (request.FromFinancialAccountId == request.ToFinancialAccountId)
            throw new InvalidOperationException("Source and destination Financial Accounts must be different.");
        if (request.Amount <= 0m)
            throw new InvalidOperationException("Transfer amount must be greater than zero.");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidOperationException("Idempotency key is required.");

        var existing = await _db.LedgerTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.IdempotencyScope == "TreasuryInternalLiquidityTransfer" &&
                x.IdempotencyKey == idempotencyKey &&
                !x.IsDeleted, ct);

        if (existing is not null)
        {
            var postings = await _db.LedgerPostings.AsNoTracking()
                .Where(x => x.LedgerTransactionId == existing.Id)
                .ToListAsync(ct);

            var debit = postings.Single(x => x.Side == LedgerPostingSide.Debit);
            var credit = postings.Single(x => x.Side == LedgerPostingSide.Credit);

            return new InternalLiquidityTransferResultDto
            {
                LedgerTransactionId = existing.Id,
                Reference = existing.Reference,
                AssetCode = existing.AssetCode,
                FromFinancialAccountId = debit.FinancialAccountId,
                ToFinancialAccountId = credit.FinancialAccountId,
                Amount = existing.Amount,
                SourceAvailableAfter = debit.AccountBalanceAfter ?? 0m,
                DestinationAvailableAfter = credit.AccountBalanceAfter ?? 0m,
                PostedAt = existing.PostedAt
            };
        }

        await using var tx = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        var accounts = await _db.FinancialAccounts
            .Where(x =>
                (x.Id == request.FromFinancialAccountId ||
                 x.Id == request.ToFinancialAccountId) &&
                !x.IsDeleted)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        var source = accounts.SingleOrDefault(x => x.Id == request.FromFinancialAccountId)
            ?? throw new InvalidOperationException("Source Financial Account not found.");
        var destination = accounts.SingleOrDefault(x => x.Id == request.ToFinancialAccountId)
            ?? throw new InvalidOperationException("Destination Financial Account not found.");

        if (source.Status != FinancialAccountStatus.Active ||
            destination.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Both Financial Accounts must be active.");

        if (!source.AssetCode.Equals(destination.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Internal liquidity transfers require the same asset.");

        if (!LiquidityAccountTypes.Contains(source.AccountType) ||
            !LiquidityAccountTypes.Contains(destination.AccountType))
            throw new InvalidOperationException("Internal liquidity transfers are limited to treasury liquidity account types.");

        if (source.AvailableBalance < request.Amount ||
            source.SettledBalance < request.Amount)
            throw new InvalidOperationException("Source Financial Account has insufficient settled available liquidity.");

        source.AvailableBalance -= request.Amount;
        source.SettledBalance -= request.Amount;
        source.LastUpdatedAt = DateTime.UtcNow;
        source.LastUpdatedByUserId = userId;

        destination.AvailableBalance += request.Amount;
        destination.SettledBalance += request.Amount;
        destination.LastUpdatedAt = DateTime.UtcNow;
        destination.LastUpdatedByUserId = userId;

        var now = DateTime.UtcNow;
        var ledger = new LedgerTransaction
        {
            Reference = $"KXTRSY-{now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            AssetCode = source.AssetCode,
            Type = LedgerTransactionType.Treasury,
            Status = LedgerTransactionStatus.Posted,
            Amount = request.Amount,
            Description = request.Reason.Trim(),
            IdempotencyScope = "TreasuryInternalLiquidityTransfer",
            IdempotencyKey = idempotencyKey,
            RelatedEntityType = nameof(FinancialAccount),
            RelatedEntityId = source.Id,
            ContextEntityType = nameof(FinancialAccount),
            ContextEntityId = destination.Id,
            PostedAt = now,
            CreatedByUserId = userId
        };

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransaction = ledger,
            LedgerTransactionId = ledger.Id,
            FinancialAccountId = source.Id,
            FinancialAccount = source,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Debit,
            Amount = request.Amount,
            AccountBalanceAfter = source.AvailableBalance
        });

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransaction = ledger,
            LedgerTransactionId = ledger.Id,
            FinancialAccountId = destination.Id,
            FinancialAccount = destination,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = request.Amount,
            AccountBalanceAfter = destination.AvailableBalance
        });

        _db.LedgerTransactions.Add(ledger);

        await _audit.RecordAsync(new AuditRecordRequest(
            "TreasuryInternalLiquidityTransferred",
            "Treasury",
            nameof(LedgerTransaction),
            ledger.Id.ToString(),
            NewValues: new
            {
                SourceAccountId = source.Id,
                SourceAccountType = source.AccountType,
                DestinationAccountId = destination.Id,
                DestinationAccountType = destination.AccountType,
                source.AssetCode,
                request.Amount,
                request.Reason,
                IdempotencyKey = idempotencyKey
            },
            UserId: userId == Guid.Empty ? null : userId), ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new InternalLiquidityTransferResultDto
        {
            LedgerTransactionId = ledger.Id,
            Reference = ledger.Reference,
            AssetCode = ledger.AssetCode,
            FromFinancialAccountId = source.Id,
            ToFinancialAccountId = destination.Id,
            Amount = request.Amount,
            SourceAvailableAfter = source.AvailableBalance,
            DestinationAvailableAfter = destination.AvailableBalance,
            PostedAt = ledger.PostedAt
        };
    }

    private static LiquidityPositionStatus ResolveUnifiedLiquidityStatus(
        decimal balance,
        bool active,
        LiquidityThreshold? threshold)
    {
        if (!active) return LiquidityPositionStatus.Critical;
        if (threshold is null) return LiquidityPositionStatus.Unknown;
        if (balance < threshold.MinimumBalance)
            return balance <= 0m ? LiquidityPositionStatus.Critical : LiquidityPositionStatus.Low;
        if (threshold.MaximumBalance.HasValue && balance > threshold.MaximumBalance.Value)
            return LiquidityPositionStatus.AboveTarget;
        if (balance > threshold.TargetBalance)
            return LiquidityPositionStatus.AboveTarget;
        return LiquidityPositionStatus.Healthy;
    }

    private static string NetworkKey(string assetCode, string? networkCode) =>
        $"{assetCode.Trim().ToUpperInvariant()}|{(networkCode ?? "").Trim().ToUpperInvariant()}";
}
