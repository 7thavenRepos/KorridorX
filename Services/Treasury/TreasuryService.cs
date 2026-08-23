using KorridorX.Providers.Remittance.Blaaiz.Models;
using System.Text;
using System.Security.Cryptography;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Models.Treasury;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Treasury;

public sealed partial class TreasuryService : ITreasuryService
{
    private readonly AppDbContext _db;
    private readonly IBlaaizApiClient _blaaiz;
    private readonly IAuditService _audit;
    private readonly TreasuryOptions _options;

    public TreasuryService(
        AppDbContext db,
        IBlaaizApiClient blaaiz,
        IAuditService audit,
        IOptions<TreasuryOptions> options)
    {
        _db = db;
        _blaaiz = blaaiz;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ProviderWalletDto>> SyncProviderWalletsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var result = await _blaaiz.ListWalletsAsync(ct);
        var now = DateTime.UtcNow;
        var returnedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var providerWallet in result.Data)
        {
            if (string.IsNullOrWhiteSpace(providerWallet.Id) || string.IsNullOrWhiteSpace(providerWallet.Currency))
                continue;

            returnedIds.Add(providerWallet.Id);
            var wallet = await _db.ProviderWalletBalances.FirstOrDefaultAsync(x =>
                x.ProviderCode == "Blaaiz" &&
                x.ProviderWalletId == providerWallet.Id &&
                !x.IsDeleted,
                ct);

            if (wallet is null)
            {
                wallet = new ProviderWalletBalance
                {
                    ProviderCode = "Blaaiz",
                    ProviderWalletId = providerWallet.Id,
                    ProviderBusinessId = providerWallet.BusinessId,
                    CurrencyCode = NormalizeCode(providerWallet.Currency),
                    Balance = providerWallet.Amount,
                    IsActive = providerWallet.IsActive,
                    LastSyncedAt = now
                };
                _db.ProviderWalletBalances.Add(wallet);
            }
            else
            {
                wallet.ProviderBusinessId = providerWallet.BusinessId;
                wallet.CurrencyCode = NormalizeCode(providerWallet.Currency);
                wallet.Balance = providerWallet.Amount;
                wallet.IsActive = providerWallet.IsActive;
                wallet.LastSyncedAt = now;
                wallet.LastUpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);

        var localWallets = await _db.ProviderWalletBalances
            .Where(x => x.ProviderCode == "Blaaiz" && !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var wallet in localWallets.Where(x => !returnedIds.Contains(x.ProviderWalletId)))
        {
            wallet.IsActive = false;
            wallet.LastUpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuditRecordRequest(
            "ProviderWalletsSynced",
            "Treasury",
            "ProviderWalletBalance",
            Metadata: new { Provider = "Blaaiz", Count = result.Data.Count },
            UserId: userId == Guid.Empty ? null : userId), ct);

        var thresholds = await GetThresholdMapAsync(ct);
        return localWallets
            .OrderBy(x => x.CurrencyCode)
            .Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode))))
            .ToList();
    }

    public async Task<TreasuryDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-_options.ProviderWalletStaleMinutes);
        var wallets = await _db.ProviderWalletBalances.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CurrencyCode)
            .ToListAsync(ct);
        var thresholds = await GetThresholdMapAsync(ct);
        var walletDtos = wallets
            .Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode))))
            .ToList();

        return new TreasuryDashboardDto
        {
            GeneratedAt = now,
            ProviderWalletCount = wallets.Count,
            LowLiquidityWalletCount = walletDtos.Count(x =>
                x.LiquidityStatus is LiquidityPositionStatus.Low or LiquidityPositionStatus.Critical),
            StaleProviderWalletCount = wallets.Count(x => x.LastSyncedAt < staleBefore),
            ActiveSettlementBatches = await _db.SettlementBatches.CountAsync(x =>
                !x.IsDeleted &&
                x.Status != SettlementBatchStatus.Reconciled &&
                x.Status != SettlementBatchStatus.Closed,
                ct),
            SettlementVarianceCount = await _db.SettlementBatches.CountAsync(x =>
                !x.IsDeleted && x.Status == SettlementBatchStatus.Variance,
                ct),
            Wallets = walletDtos
        };
    }

    public async Task<PagedResult<ProviderWalletDto>> GetProviderWalletsAsync(
        string? currencyCode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ProviderWalletBalances.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var code = NormalizeCode(currencyCode);
            query = query.Where(x => x.CurrencyCode == code);
        }

        var paged = await query.OrderBy(x => x.CurrencyCode).ThenBy(x => x.ProviderWalletId).PaginateAsync(page, pageSize, ct);
        var thresholds = await GetThresholdMapAsync(ct);
        return new PagedResult<ProviderWalletDto>
        {
            Meta = paged.Meta,
            Items = paged.Items.Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode)))).ToList()
        };
    }

    public async Task<LiquidityThresholdDto> UpsertThresholdAsync(
        Guid userId,
        UpsertLiquidityThresholdRequestDto request,
        CancellationToken ct = default)
    {
        var scopeType = request.ScopeType;
        var providerCode = scopeType == TreasuryLiquidityScopeType.FinancialAccount
            ? "INTERNAL"
            : Clean(request.ProviderCode, 50);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        var networkCode = CleanNullable(request.NetworkCode, 50)?.ToUpperInvariant();

        if (scopeType == TreasuryLiquidityScopeType.FinancialAccount)
        {
            if (!request.FinancialAccountType.HasValue ||
                request.FinancialAccountType.Value is not (
                    FinancialAccountType.House or
                    FinancialAccountType.Treasury or
                    FinancialAccountType.ProviderClearing or
                    FinancialAccountType.Settlement))
            {
                throw new InvalidOperationException(
                    "Financial-account liquidity thresholds require House, Treasury, ProviderClearing, or Settlement account type.");
            }

            networkCode = null;
        }
        else
        {
            request.FinancialAccountType = null;
        }

        if (request.TargetBalance < request.MinimumBalance)
            throw new InvalidOperationException("Target balance cannot be below the minimum balance.");
        if (request.MaximumBalance.HasValue && request.MaximumBalance.Value < request.TargetBalance)
            throw new InvalidOperationException("Maximum balance cannot be below the target balance.");

        var existing = await _db.LiquidityThresholds.FirstOrDefaultAsync(x =>
            x.ScopeType == scopeType &&
            x.ProviderCode == providerCode &&
            x.CurrencyCode == currencyCode &&
            x.NetworkCode == networkCode &&
            x.FinancialAccountType == request.FinancialAccountType &&
            x.IsActive &&
            !x.IsDeleted,
            ct);

        if (existing is null)
        {
            existing = new LiquidityThreshold
            {
                ScopeType = scopeType,
                ProviderCode = providerCode,
                CurrencyCode = currencyCode,
                NetworkCode = networkCode,
                FinancialAccountType = request.FinancialAccountType,
                CreatedByUserId = userId
            };
            _db.LiquidityThresholds.Add(existing);
        }

        existing.MinimumBalance = request.MinimumBalance;
        existing.TargetBalance = request.TargetBalance;
        existing.MaximumBalance = request.MaximumBalance;
        existing.IsActive = request.IsActive;
        existing.LastUpdatedAt = DateTime.UtcNow;
        existing.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuditRecordRequest(
            "LiquidityThresholdUpserted",
            "Treasury",
            nameof(LiquidityThreshold),
            existing.Id.ToString(),
            NewValues: request,
            UserId: userId == Guid.Empty ? null : userId), ct);

        return ToThresholdDto(existing);
    }

    public async Task<PagedResult<LiquidityThresholdDto>> GetThresholdsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var paged = await _db.LiquidityThresholds.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ProviderCode)
            .ThenBy(x => x.CurrencyCode)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<LiquidityThresholdDto>
        {
            Meta = paged.Meta,
            Items = paged.Items.Select(ToThresholdDto).ToList()
        };
    }

    public async Task<SettlementBatchDto> CreateSettlementBatchAsync(
        Guid userId,
        CreateSettlementBatchRequestDto request,
        CancellationToken ct = default)
    {
        var provider = ParseProviderCode(request.ProviderCode);
        var providerCode = provider.ToString();
        var currencyCode = NormalizeCode(request.CurrencyCode);
        var start = DateTime.SpecifyKind(request.WindowStart, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(request.WindowEnd, DateTimeKind.Utc);
        if (end <= start) throw new InvalidOperationException("Settlement window end must be after the start.");
        if (end > DateTime.UtcNow.AddMinutes(5)) throw new InvalidOperationException("Settlement window cannot end in the future.");

        var usedRows = _db.SettlementBatchItems.AsNoTracking().Select(x => x.ProviderTransactionRowId);
        var transactions = await _db.ProviderTransactions.AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.ProviderCode == provider &&
                x.CurrencyCode == currencyCode &&
                !usedRows.Contains(x.Id) &&
                (x.ProviderCreatedAt ?? x.CreatedAt) >= start &&
                (x.ProviderCreatedAt ?? x.CreatedAt) < end &&
                (x.ProviderStatus.ToUpper() == "SUCCESSFUL" ||
                 x.ProviderStatus.ToUpper() == "COMPLETED" ||
                 x.ProviderStatus.ToUpper() == "REFUNDED"))
            .OrderBy(x => x.ProviderCreatedAt ?? x.CreatedAt)
            .ToListAsync(ct);

        if (transactions.Count == 0)
            throw new InvalidOperationException("No unsettled successful provider transactions were found in the requested window.");

        var batch = new SettlementBatch
        {
            Reference = GenerateSettlementReference(),
            ProviderCode = providerCode,
            CurrencyCode = currencyCode,
            WindowStart = start,
            WindowEnd = end,
            Status = SettlementBatchStatus.PendingReconciliation,
            CreatedByUserId = userId
        };

        foreach (var transaction in transactions)
        {
            var direction = ResolveDirection(transaction);
            var item = new SettlementBatchItem
            {
                SettlementBatchId = batch.Id,
                ProviderTransactionRowId = transaction.Id,
                Direction = direction,
                Amount = transaction.Amount,
                CurrencyCode = currencyCode
            };
            batch.Items.Add(item);
            if (direction == SettlementDirection.Inflow) batch.GrossInflows += transaction.Amount;
            else batch.GrossOutflows += transaction.Amount;
        }

        batch.ExpectedNetAmount = batch.GrossInflows - batch.GrossOutflows;
        _db.SettlementBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuditRecordRequest(
            "SettlementBatchCreated",
            "Treasury",
            nameof(SettlementBatch),
            batch.Id.ToString(),
            NewValues: new { batch.Reference, batch.ProviderCode, batch.CurrencyCode, batch.WindowStart, batch.WindowEnd, batch.ExpectedNetAmount, ItemCount = batch.Items.Count },
            UserId: userId == Guid.Empty ? null : userId), ct);

        return await LoadBatchDtoAsync(batch.Id, ct);
    }

    public async Task<SettlementBatchDto> ReconcileSettlementBatchAsync(
        Guid userId,
        Guid batchId,
        ReconcileSettlementBatchRequestDto request,
        CancellationToken ct = default)
    {
        var batch = await _db.SettlementBatches.FirstOrDefaultAsync(x => x.Id == batchId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Settlement batch not found.");
        if (batch.Status == SettlementBatchStatus.Closed)
            throw new InvalidOperationException("Closed settlement batches cannot be reconciled again.");

        batch.ActualNetAmount = request.ActualNetAmount;
        batch.VarianceAmount = request.ActualNetAmount - batch.ExpectedNetAmount;
        batch.ReconciliationNote = CleanNullable(request.Note, 2000);
        batch.ReconciledAt = DateTime.UtcNow;
        batch.Status = Math.Abs(batch.VarianceAmount.Value) <= _options.SettlementVarianceTolerance
            ? SettlementBatchStatus.Reconciled
            : SettlementBatchStatus.Variance;
        batch.LastUpdatedAt = DateTime.UtcNow;
        batch.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuditRecordRequest(
            "SettlementBatchReconciled",
            "Treasury",
            nameof(SettlementBatch),
            batch.Id.ToString(),
            NewValues: new { batch.ExpectedNetAmount, batch.ActualNetAmount, batch.VarianceAmount, batch.Status, batch.ReconciliationNote },
            UserId: userId == Guid.Empty ? null : userId), ct);

        return await LoadBatchDtoAsync(batch.Id, ct);
    }

    public async Task<PagedResult<SettlementBatchDto>> GetSettlementBatchesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var paged = await _db.SettlementBatches.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Include(x => x.Items).ThenInclude(x => x.ProviderTransaction)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<SettlementBatchDto>
        {
            Meta = paged.Meta,
            Items = paged.Items.Select(ToSettlementDto).ToList()
        };
    }

    public async Task<IReadOnlyList<TreasuryRebalanceSuggestionDto>> GetRebalanceSuggestionsAsync(CancellationToken ct = default)
    {
        var wallets = await _db.ProviderWalletBalances.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .ToListAsync(ct);
        var thresholds = await GetThresholdMapAsync(ct);

        var sources = wallets
            .Select(x => new { Wallet = x, Threshold = thresholds.GetValueOrDefault(ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode)) })
            .Where(x => x.Threshold is not null && x.Wallet.Balance > x.Threshold.TargetBalance)
            .ToList();

        var destinations = wallets
            .Select(x => new { Wallet = x, Threshold = thresholds.GetValueOrDefault(ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode)) })
            .Where(x => x.Threshold is not null && x.Wallet.Balance < x.Threshold.TargetBalance)
            .ToList();

        var suggestions = new List<TreasuryRebalanceSuggestionDto>();
        foreach (var destination in destinations)
        {
            var source = sources
                .Where(x => x.Wallet.ProviderCode.Equals(destination.Wallet.ProviderCode, StringComparison.OrdinalIgnoreCase)
                    && !x.Wallet.CurrencyCode.Equals(destination.Wallet.CurrencyCode, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(x.Wallet.ProviderBusinessId)
                        || string.IsNullOrWhiteSpace(destination.Wallet.ProviderBusinessId)
                        || x.Wallet.ProviderBusinessId == destination.Wallet.ProviderBusinessId))
                .OrderByDescending(x => x.Wallet.Balance - x.Threshold!.TargetBalance)
                .FirstOrDefault();

            if (source is null) continue;
            suggestions.Add(new TreasuryRebalanceSuggestionDto
            {
                FromProviderWalletBalanceId = source.Wallet.Id,
                ToProviderWalletBalanceId = destination.Wallet.Id,
                ProviderCode = source.Wallet.ProviderCode,
                FromCurrencyCode = source.Wallet.CurrencyCode,
                ToCurrencyCode = destination.Wallet.CurrencyCode,
                SourceExcessAboveTarget = source.Wallet.Balance - source.Threshold!.TargetBalance,
                DestinationShortfallToTarget = destination.Threshold!.TargetBalance - destination.Wallet.Balance
            });
        }

        return suggestions;
    }

    public async Task<TreasuryRebalanceDto> CreateRebalanceAsync(
        Guid userId,
        CreateTreasuryRebalanceRequestDto request,
        CancellationToken ct = default)
    {
        if (request.FromProviderWalletBalanceId == request.ToProviderWalletBalanceId)
            throw new InvalidOperationException("Source and destination provider wallets must be different.");
        if (request.Amount < _options.MinimumSwapAmount)
            throw new InvalidOperationException($"Swap amount must be at least {_options.MinimumSwapAmount:0.##}.");
        if (decimal.Round(request.Amount, 2) != request.Amount)
            throw new InvalidOperationException("Swap amount supports a maximum of two decimal places.");

        var fromWallet = await _db.ProviderWalletBalances.FirstOrDefaultAsync(x =>
            x.Id == request.FromProviderWalletBalanceId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Source provider wallet not found.");
        var toWallet = await _db.ProviderWalletBalances.FirstOrDefaultAsync(x =>
            x.Id == request.ToProviderWalletBalanceId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Destination provider wallet not found.");

        if (!fromWallet.IsActive || !toWallet.IsActive)
            throw new InvalidOperationException("Both provider wallets must be active.");
        if (!fromWallet.ProviderCode.Equals(toWallet.ProviderCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider wallet swaps must use wallets from the same provider.");
        if (!fromWallet.ProviderCode.Equals("Blaaiz", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Live treasury swaps are currently implemented only for Blaaiz provider wallets.");
        if (fromWallet.CurrencyCode.Equals(toWallet.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider wallet swaps require different currencies.");
        if (!string.IsNullOrWhiteSpace(fromWallet.ProviderBusinessId) &&
            !string.IsNullOrWhiteSpace(toWallet.ProviderBusinessId) &&
            fromWallet.ProviderBusinessId != toWallet.ProviderBusinessId)
            throw new InvalidOperationException("Provider wallets must belong to the same provider business.");
        if (request.AmountType == TreasurySwapAmountType.From && fromWallet.Balance < request.Amount)
            throw new InvalidOperationException("Source provider wallet does not have enough synchronized balance for this swap.");

        var entity = new TreasuryRebalanceRequest
        {
            Reference = GenerateRebalanceReference(),
            ProviderCode = fromWallet.ProviderCode,
            FromProviderWalletBalanceId = fromWallet.Id,
            ToProviderWalletBalanceId = toWallet.Id,
            FromCurrencyCode = fromWallet.CurrencyCode,
            ToCurrencyCode = toWallet.CurrencyCode,
            RequestedAmount = request.Amount,
            AmountType = request.AmountType,
            Status = _options.RebalanceApprovalRequired ? TreasuryRebalanceStatus.PendingApproval : TreasuryRebalanceStatus.Approved,
            Reason = CleanNullable(request.Reason, 1000),
            RequestedByUserId = userId,
            CreatedByUserId = userId
        };

        if (!_options.RebalanceApprovalRequired)
        {
            entity.ApprovedByUserId = userId;
            entity.ApprovedAt = DateTime.UtcNow;
        }

        _db.TreasuryRebalanceRequests.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(new AuditRecordRequest(
            "TreasuryRebalanceRequested", "Treasury", nameof(TreasuryRebalanceRequest), entity.Id.ToString(),
            NewValues: new { entity.Reference, entity.FromCurrencyCode, entity.ToCurrencyCode, entity.RequestedAmount, entity.AmountType, entity.Status },
            UserId: userId), ct);

        return ToRebalanceDto(entity);
    }

    public async Task<TreasuryRebalanceDto> ReviewRebalanceAsync(
        Guid userId,
        Guid rebalanceId,
        ReviewTreasuryRebalanceRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await _db.TreasuryRebalanceRequests.FirstOrDefaultAsync(x => x.Id == rebalanceId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Treasury rebalance request not found.");
        if (entity.Status != TreasuryRebalanceStatus.PendingApproval)
            throw new InvalidOperationException("Only pending treasury rebalances can be reviewed.");
        if (entity.RequestedByUserId == userId)
            throw new InvalidOperationException("The user who requested a treasury rebalance cannot approve or reject the same request.");

        if (request.Approve)
        {
            entity.Status = TreasuryRebalanceStatus.Approved;
            entity.ApprovedByUserId = userId;
            entity.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Note))
                throw new InvalidOperationException("A rejection reason is required.");
            entity.Status = TreasuryRebalanceStatus.Rejected;
            entity.RejectedByUserId = userId;
            entity.RejectedAt = DateTime.UtcNow;
            entity.RejectionReason = CleanNullable(request.Note, 1000);
        }

        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(new AuditRecordRequest(
            request.Approve ? "TreasuryRebalanceApproved" : "TreasuryRebalanceRejected",
            "Treasury", nameof(TreasuryRebalanceRequest), entity.Id.ToString(),
            NewValues: new { entity.Status, Note = request.Note }, UserId: userId), ct);
        return ToRebalanceDto(entity);
    }

    public async Task<TreasuryRebalanceDto> ExecuteRebalanceAsync(
        Guid userId,
        Guid rebalanceId,
        CancellationToken ct = default)
    {
        var entity = await _db.TreasuryRebalanceRequests
            .Include(x => x.FromProviderWalletBalance)
            .Include(x => x.ToProviderWalletBalance)
            .FirstOrDefaultAsync(x => x.Id == rebalanceId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Treasury rebalance request not found.");
        if (entity.Status != TreasuryRebalanceStatus.Approved && entity.Status != TreasuryRebalanceStatus.Failed)
            throw new InvalidOperationException("Only approved or previously failed treasury rebalances can be executed.");
        if (!entity.FromProviderWalletBalance.IsActive || !entity.ToProviderWalletBalance.IsActive)
            throw new InvalidOperationException("Both provider wallets must be active before execution.");

        entity.Status = TreasuryRebalanceStatus.Executing;
        entity.FailureReason = null;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await _blaaiz.SwapBusinessWalletsAsync(new BlaaizSwapRequest
            {
                FromBusinessWalletId = entity.FromProviderWalletBalance.ProviderWalletId,
                ToBusinessWalletId = entity.ToProviderWalletBalance.ProviderWalletId,
                Amount = entity.RequestedAmount,
                AmountType = entity.AmountType == TreasurySwapAmountType.To ? "to" : "from"
            }, ct);

            var swap = result.Data.BusinessSwapTransaction;
            if (!swap.Status.Equals("SUCCESSFUL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Blaaiz returned swap status '{swap.Status}'.");

            entity.ProviderSwapId = swap.Id;
            entity.ProviderTransactionId = swap.BusinessTransactionId;
            entity.ProviderReference = swap.Reference;
            entity.FromAmount = swap.FromAmount;
            entity.FromAmountMinusFees = swap.FromAmountMinusFees;
            entity.ToAmount = swap.ToAmount;
            entity.ExchangeRate = swap.FromExchangeRate;
            entity.CustomExchangeRate = swap.CustomExchangeRate;
            entity.Status = TreasuryRebalanceStatus.Completed;
            entity.ExecutedAt = DateTime.UtcNow;
            entity.LastUpdatedAt = DateTime.UtcNow;

            entity.FromProviderWalletBalance.Balance = Math.Max(0m, entity.FromProviderWalletBalance.Balance - swap.FromAmount);
            entity.ToProviderWalletBalance.Balance += swap.ToAmount;
            entity.FromProviderWalletBalance.LastSyncedAt = DateTime.UtcNow;
            entity.ToProviderWalletBalance.LastSyncedAt = DateTime.UtcNow;
            entity.FromProviderWalletBalance.LastUpdatedAt = DateTime.UtcNow;
            entity.ToProviderWalletBalance.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAsync(new AuditRecordRequest(
                "TreasuryRebalanceExecuted", "Treasury", nameof(TreasuryRebalanceRequest), entity.Id.ToString(),
                NewValues: new { entity.ProviderSwapId, entity.ProviderTransactionId, entity.FromAmount, entity.ToAmount, entity.ExchangeRate },
                UserId: userId), ct);
            return ToRebalanceDto(entity);
        }
        catch (Exception ex)
        {
            entity.Status = TreasuryRebalanceStatus.Failed;
            entity.FailedAt = DateTime.UtcNow;
            entity.FailureReason = CleanNullable(ex.Message, 2000);
            entity.LastUpdatedAt = DateTime.UtcNow;
            entity.LastUpdatedByUserId = userId;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResult<TreasuryRebalanceDto>> GetRebalancesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var paged = await _db.TreasuryRebalanceRequests.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<TreasuryRebalanceDto>
        {
            Meta = paged.Meta,
            Items = paged.Items.Select(ToRebalanceDto).ToList()
        };
    }

    public async Task<SettlementStatementImportDto> ImportSettlementStatementAsync(
        Guid userId,
        Guid batchId,
        ImportSettlementStatementFormDto request,
        CancellationToken ct = default)
    {
        if (request.File is null || request.File.Length == 0)
            throw new InvalidOperationException("A settlement statement CSV file is required.");
        if (request.File.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Settlement statement file cannot exceed 10 MB.");
        if (!Path.GetExtension(request.File.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Settlement statement must be a CSV file.");

        var batch = await _db.SettlementBatches
            .Include(x => x.Items).ThenInclude(x => x.ProviderTransaction)
            .FirstOrDefaultAsync(x => x.Id == batchId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Settlement batch not found.");

        await using var memory = new MemoryStream();
        await request.File.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var fileHash = Convert.ToHexString(SHA256.HashData(bytes));
        if (await _db.SettlementStatementImports.AnyAsync(x => x.ProviderCode == batch.ProviderCode && x.FileHash == fileHash && !x.IsDeleted, ct))
            throw new InvalidOperationException("This settlement statement has already been imported.");

        memory.Position = 0;
        using var reader = new StreamReader(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(ct) ?? throw new InvalidOperationException("Settlement statement is empty.");
        var headers = ParseCsvLine(headerLine).Select((x, i) => new { Name = x.Trim().ToLowerInvariant(), Index = i })
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "provider_transaction_id", "direction", "amount", "currency" })
            if (!headers.ContainsKey(required)) throw new InvalidOperationException($"Settlement statement is missing required column '{required}'.");

        var batchTransactions = batch.Items.ToDictionary(x => x.ProviderTransaction.ProviderTransactionId, StringComparer.OrdinalIgnoreCase);
        var import = new SettlementStatementImport
        {
            SettlementBatchId = batch.Id,
            ProviderCode = batch.ProviderCode,
            CurrencyCode = batch.CurrencyCode,
            FileName = Path.GetFileName(request.File.FileName),
            FileHash = fileHash,
            Note = CleanNullable(request.Note, 2000),
            ImportedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        var rowNumber = 1;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseCsvLine(line);
            string Get(string name) => headers[name] < values.Count ? values[headers[name]].Trim() : "";
            string? GetOptional(string name) => headers.TryGetValue(name, out var idx) && idx < values.Count && !string.IsNullOrWhiteSpace(values[idx]) ? values[idx].Trim() : null;

            var providerTransactionId = Get("provider_transaction_id");
            if (string.IsNullOrWhiteSpace(providerTransactionId)) throw new InvalidOperationException($"Row {rowNumber}: provider_transaction_id is required.");
            if (!decimal.TryParse(Get("amount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0)
                throw new InvalidOperationException($"Row {rowNumber}: amount is invalid.");
            var currency = NormalizeCode(Get("currency"));
            if (currency != batch.CurrencyCode) throw new InvalidOperationException($"Row {rowNumber}: currency '{currency}' does not match settlement batch currency '{batch.CurrencyCode}'.");
            var directionText = Get("direction").ToLowerInvariant();
            var direction = directionText is "credit" or "inflow" ? SettlementDirection.Inflow
                : directionText is "debit" or "outflow" ? SettlementDirection.Outflow
                : throw new InvalidOperationException($"Row {rowNumber}: direction must be credit/inflow or debit/outflow.");

            batchTransactions.TryGetValue(providerTransactionId, out var matchedBatchItem);
            DateTime? occurredAt = null;
            var occurredText = GetOptional("occurred_at");
            if (!string.IsNullOrWhiteSpace(occurredText) && DateTimeOffset.TryParse(occurredText, out var parsedOccurred))
                occurredAt = parsedOccurred.UtcDateTime;

            var item = new SettlementStatementItem
            {
                RowNumber = rowNumber,
                ProviderTransactionId = providerTransactionId,
                ProviderReference = GetOptional("provider_reference"),
                TransactionType = GetOptional("transaction_type") ?? "",
                Direction = direction,
                Amount = amount,
                CurrencyCode = currency,
                ProviderStatus = GetOptional("status"),
                OccurredAt = occurredAt,
                IsMatched = matchedBatchItem is not null,
                ProviderTransactionRowId = matchedBatchItem?.ProviderTransactionRowId
            };
            import.Items.Add(item);
            if (direction == SettlementDirection.Inflow) import.GrossCredits += amount;
            else import.GrossDebits += amount;
        }

        if (import.Items.Count == 0) throw new InvalidOperationException("Settlement statement contains no transaction rows.");
        import.RowCount = import.Items.Count;
        import.MatchedRowCount = import.Items.Count(x => x.IsMatched);
        import.UnmatchedRowCount = import.RowCount - import.MatchedRowCount;
        import.NetAmount = import.GrossCredits - import.GrossDebits;
        import.VarianceAmount = import.NetAmount - batch.ExpectedNetAmount;
        import.Status = import.UnmatchedRowCount == 0 && Math.Abs(import.VarianceAmount.Value) <= _options.SettlementVarianceTolerance
            ? SettlementStatementImportStatus.Reconciled
            : SettlementStatementImportStatus.Variance;

        batch.ActualNetAmount = import.NetAmount;
        batch.VarianceAmount = import.VarianceAmount;
        batch.ReconciliationNote = CleanNullable(request.Note, 2000);
        batch.ReconciledAt = DateTime.UtcNow;
        batch.Status = import.Status == SettlementStatementImportStatus.Reconciled ? SettlementBatchStatus.Reconciled : SettlementBatchStatus.Variance;
        batch.LastUpdatedAt = DateTime.UtcNow;
        batch.LastUpdatedByUserId = userId;

        _db.SettlementStatementImports.Add(import);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(new AuditRecordRequest(
            "SettlementStatementImported", "Treasury", nameof(SettlementStatementImport), import.Id.ToString(),
            NewValues: new { import.FileName, import.RowCount, import.MatchedRowCount, import.UnmatchedRowCount, import.NetAmount, import.VarianceAmount, import.Status },
            Metadata: new { SettlementBatchId = batch.Id, batch.Reference }, UserId: userId), ct);
        return ToStatementImportDto(import);
    }

    private async Task<SettlementBatchDto> LoadBatchDtoAsync(Guid id, CancellationToken ct)
    {
        var batch = await _db.SettlementBatches.AsNoTracking()
            .Include(x => x.Items).ThenInclude(x => x.ProviderTransaction)
            .FirstAsync(x => x.Id == id, ct);
        return ToSettlementDto(batch);
    }

    private async Task<Dictionary<string, LiquidityThreshold>> GetThresholdMapAsync(CancellationToken ct)
    {
        var thresholds = await _db.LiquidityThresholds.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return thresholds
            .Where(x => x.ScopeType == TreasuryLiquidityScopeType.ProviderWallet)
            .GroupBy(
                x => ProviderThresholdKey(x.ProviderCode, x.CurrencyCode, x.NetworkCode),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static ProviderWalletDto ToWalletDto(ProviderWalletBalance wallet, LiquidityThreshold? threshold) => new()
    {
        Id = wallet.Id,
        ProviderCode = wallet.ProviderCode,
        ProviderWalletId = wallet.ProviderWalletId,
        CurrencyCode = wallet.CurrencyCode,
        Balance = wallet.Balance,
        IsActive = wallet.IsActive,
        LastSyncedAt = wallet.LastSyncedAt,
        MinimumBalance = threshold?.MinimumBalance,
        TargetBalance = threshold?.TargetBalance,
        MaximumBalance = threshold?.MaximumBalance,
        LiquidityStatus = ResolveLiquidityStatus(wallet, threshold)
    };

    private static LiquidityPositionStatus ResolveLiquidityStatus(ProviderWalletBalance wallet, LiquidityThreshold? threshold)
    {
        if (!wallet.IsActive) return LiquidityPositionStatus.Critical;
        if (threshold is null) return LiquidityPositionStatus.Unknown;
        if (wallet.Balance < threshold.MinimumBalance) return wallet.Balance <= 0 ? LiquidityPositionStatus.Critical : LiquidityPositionStatus.Low;
        if (threshold.MaximumBalance.HasValue && wallet.Balance > threshold.MaximumBalance.Value) return LiquidityPositionStatus.AboveTarget;
        return LiquidityPositionStatus.Healthy;
    }

    private static LiquidityThresholdDto ToThresholdDto(LiquidityThreshold x) => new()
    {
        Id = x.Id,
        ScopeType = x.ScopeType,
        ProviderCode = x.ProviderCode,
        CurrencyCode = x.CurrencyCode,
        NetworkCode = x.NetworkCode,
        FinancialAccountType = x.FinancialAccountType,
        MinimumBalance = x.MinimumBalance,
        TargetBalance = x.TargetBalance,
        MaximumBalance = x.MaximumBalance,
        IsActive = x.IsActive
    };

    private static SettlementBatchDto ToSettlementDto(SettlementBatch x) => new()
    {
        Id = x.Id, Reference = x.Reference, ProviderCode = x.ProviderCode, CurrencyCode = x.CurrencyCode,
        WindowStart = x.WindowStart, WindowEnd = x.WindowEnd, Status = x.Status,
        GrossInflows = x.GrossInflows, GrossOutflows = x.GrossOutflows, ExpectedNetAmount = x.ExpectedNetAmount,
        ActualNetAmount = x.ActualNetAmount, VarianceAmount = x.VarianceAmount,
        ReconciliationNote = x.ReconciliationNote, ReconciledAt = x.ReconciledAt, CreatedAt = x.CreatedAt,
        Items = x.Items.OrderBy(y => y.CreatedAt).Select(y => new SettlementBatchItemDto
        {
            Id = y.Id, ProviderTransactionRowId = y.ProviderTransactionRowId,
            ProviderTransactionId = y.ProviderTransaction.ProviderTransactionId,
            TransactionType = y.ProviderTransaction.TransactionType,
            Direction = y.Direction, Amount = y.Amount, CurrencyCode = y.CurrencyCode
        }).ToList()
    };

    private static SettlementDirection ResolveDirection(ProviderTransaction x)
    {
        var type = x.TransactionType.Trim().ToLowerInvariant();
        if (x.PayoutId.HasValue || type.Contains("payout") || type.Contains("refund")) return SettlementDirection.Outflow;
        return SettlementDirection.Inflow;
    }

    private static ProviderCode ParseProviderCode(string value) =>
        Enum.TryParse<ProviderCode>(value?.Trim(), true, out var provider)
            ? provider
            : throw new InvalidOperationException($"Unsupported provider '{value}'.");

    private static string Key(string provider, string currency) =>
        ProviderThresholdKey(provider, currency, null);

    private static string ProviderThresholdKey(string provider, string assetCode, string? networkCode) =>
        $"{provider.Trim().ToUpperInvariant()}|{assetCode.Trim().ToUpperInvariant()}|{(networkCode ?? "").Trim().ToUpperInvariant()}";
    private static string NormalizeCode(string value) => Clean(value, 20).ToUpperInvariant();
    private static string Clean(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("A required value was not supplied.") : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string? CleanNullable(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static TreasuryRebalanceDto ToRebalanceDto(TreasuryRebalanceRequest x) => new()
    {
        Id = x.Id, Reference = x.Reference, ProviderCode = x.ProviderCode,
        FromProviderWalletBalanceId = x.FromProviderWalletBalanceId, ToProviderWalletBalanceId = x.ToProviderWalletBalanceId,
        FromCurrencyCode = x.FromCurrencyCode, ToCurrencyCode = x.ToCurrencyCode, RequestedAmount = x.RequestedAmount,
        AmountType = x.AmountType, Status = x.Status, Reason = x.Reason, RequestedByUserId = x.RequestedByUserId,
        ApprovedByUserId = x.ApprovedByUserId, RejectedByUserId = x.RejectedByUserId, RejectionReason = x.RejectionReason,
        ProviderSwapId = x.ProviderSwapId, ProviderTransactionId = x.ProviderTransactionId, ProviderReference = x.ProviderReference,
        FromAmount = x.FromAmount, FromAmountMinusFees = x.FromAmountMinusFees, ToAmount = x.ToAmount,
        ExchangeRate = x.ExchangeRate, CustomExchangeRate = x.CustomExchangeRate, FailureReason = x.FailureReason,
        CreatedAt = x.CreatedAt, ApprovedAt = x.ApprovedAt, RejectedAt = x.RejectedAt, ExecutedAt = x.ExecutedAt, FailedAt = x.FailedAt
    };

    private static SettlementStatementImportDto ToStatementImportDto(SettlementStatementImport x) => new()
    {
        Id = x.Id, SettlementBatchId = x.SettlementBatchId, ProviderCode = x.ProviderCode, CurrencyCode = x.CurrencyCode,
        FileName = x.FileName, Status = x.Status, RowCount = x.RowCount, MatchedRowCount = x.MatchedRowCount,
        UnmatchedRowCount = x.UnmatchedRowCount, GrossCredits = x.GrossCredits, GrossDebits = x.GrossDebits,
        NetAmount = x.NetAmount, VarianceAmount = x.VarianceAmount, Note = x.Note, ImportedAt = x.ImportedAt
    };

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        result.Add(current.ToString());
        return result;
    }

    private static string GenerateRebalanceReference() => $"KXREB-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
    private static string GenerateSettlementReference() => $"KXSET-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
}
