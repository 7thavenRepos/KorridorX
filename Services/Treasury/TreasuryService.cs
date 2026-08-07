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

public sealed class TreasuryService : ITreasuryService
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
            .Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(Key(x.ProviderCode, x.CurrencyCode))))
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
            .Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(Key(x.ProviderCode, x.CurrencyCode))))
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
            Items = paged.Items.Select(x => ToWalletDto(x, thresholds.GetValueOrDefault(Key(x.ProviderCode, x.CurrencyCode)))).ToList()
        };
    }

    public async Task<LiquidityThresholdDto> UpsertThresholdAsync(
        Guid userId,
        UpsertLiquidityThresholdRequestDto request,
        CancellationToken ct = default)
    {
        var providerCode = Clean(request.ProviderCode, 50);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        if (request.TargetBalance < request.MinimumBalance)
            throw new InvalidOperationException("Target balance cannot be below the minimum balance.");
        if (request.MaximumBalance.HasValue && request.MaximumBalance.Value < request.TargetBalance)
            throw new InvalidOperationException("Maximum balance cannot be below the target balance.");

        var existing = await _db.LiquidityThresholds.FirstOrDefaultAsync(x =>
            x.ProviderCode == providerCode &&
            x.CurrencyCode == currencyCode &&
            x.IsActive &&
            !x.IsDeleted,
            ct);

        if (existing is null)
        {
            existing = new LiquidityThreshold
            {
                ProviderCode = providerCode,
                CurrencyCode = currencyCode,
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
            .GroupBy(x => Key(x.ProviderCode, x.CurrencyCode), StringComparer.OrdinalIgnoreCase)
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
        Id = x.Id, ProviderCode = x.ProviderCode, CurrencyCode = x.CurrencyCode,
        MinimumBalance = x.MinimumBalance, TargetBalance = x.TargetBalance,
        MaximumBalance = x.MaximumBalance, IsActive = x.IsActive
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

    private static string Key(string provider, string currency) => $"{provider.Trim().ToUpperInvariant()}|{currency.Trim().ToUpperInvariant()}";
    private static string NormalizeCode(string value) => Clean(value, 10).ToUpperInvariant();
    private static string Clean(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("A required value was not supplied.") : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string? CleanNullable(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string GenerateSettlementReference() => $"KXSET-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
}
