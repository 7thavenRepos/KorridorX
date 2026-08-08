using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Finance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Finance;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Finance;

public sealed class AccountingService : IAccountingService
{
    private const string SettlementClearingCode = "1100";
    private const string TransferFeeRevenueCode = "4000";
    private const string FxSpreadRevenueCode = "4010";
    private const string SettlementVarianceGainCode = "4020";
    private const string ProviderFeeExpenseCode = "5000";
    private const string SettlementVarianceExpenseCode = "5010";
    private const string ProviderFeeAdjustmentGainCode = "4030";
    private const string IndirectTaxExpenseCode = "5030";

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AccountingService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AccountingAccountDto>> GetAccountsAsync(CancellationToken ct = default)
    {
        await EnsureSystemAccountsAsync(ct);
        return await _db.AccountingAccounts.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Code)
            .Select(x => new AccountingAccountDto
            {
                Id = x.Id, Code = x.Code, Name = x.Name, Type = x.Type,
                IsSystem = x.IsSystem, IsActive = x.IsActive, Description = x.Description
            }).ToListAsync(ct);
    }

    public async Task<AccountingAccountDto> CreateAccountAsync(Guid userId, CreateAccountingAccountRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (new[] { SettlementClearingCode, TransferFeeRevenueCode, FxSpreadRevenueCode, SettlementVarianceGainCode, ProviderFeeAdjustmentGainCode, ProviderFeeExpenseCode, SettlementVarianceExpenseCode, IndirectTaxExpenseCode }.Contains(code))
            throw new InvalidOperationException($"Accounting account code '{code}' is reserved for a system account.");
        if (await _db.AccountingAccounts.AnyAsync(x => x.Code == code && !x.IsDeleted, ct))
            throw new InvalidOperationException($"Accounting account '{code}' already exists.");

        var entity = new AccountingAccount
        {
            Code = code,
            Name = request.Name.Trim(),
            Type = request.Type,
            Description = request.Description?.Trim(),
            IsSystem = false,
            IsActive = true,
            CreatedByUserId = userId
        };
        _db.AccountingAccounts.Add(entity);
        _audit.Stage(new AuditRecordRequest("ACCOUNTING_ACCOUNT_CREATED", "Finance", nameof(AccountingAccount), entity.Id.ToString(), NewValues: new { entity.Code, entity.Name, entity.Type }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return MapAccount(entity);
    }

    public async Task<IReadOnlyList<AccountingPeriodDto>> GetPeriodsAsync(CancellationToken ct = default) =>
        await _db.AccountingPeriods.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.StartsAt)
            .Select(x => new AccountingPeriodDto
            {
                Id = x.Id, Name = x.Name, StartsAt = x.StartsAt, EndsAt = x.EndsAt, Status = x.Status,
                ClosedAt = x.ClosedAt, ClosedByUserId = x.ClosedByUserId, CloseNote = x.CloseNote
            }).ToListAsync(ct);

    public async Task<AccountingPeriodDto> CreatePeriodAsync(Guid userId, CreateAccountingPeriodRequest request, CancellationToken ct = default)
    {
        var start = ToUtc(request.StartsAt);
        var end = ToUtc(request.EndsAt);
        if (end <= start) throw new InvalidOperationException("Accounting period end must be after its start.");
        if (await _db.AccountingPeriods.AnyAsync(x => !x.IsDeleted && x.StartsAt < end && x.EndsAt > start, ct))
            throw new InvalidOperationException("Accounting periods cannot overlap.");
        var entity = new AccountingPeriod { Name = request.Name.Trim(), StartsAt = start, EndsAt = end, CreatedByUserId = userId };
        _db.AccountingPeriods.Add(entity);
        _audit.Stage(new AuditRecordRequest("ACCOUNTING_PERIOD_CREATED", "Finance", nameof(AccountingPeriod), entity.Id.ToString(), NewValues: new { entity.Name, entity.StartsAt, entity.EndsAt }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return MapPeriod(entity);
    }

    public async Task<AccountingPeriodDto> ClosePeriodAsync(Guid userId, Guid periodId, CloseAccountingPeriodRequest request, CancellationToken ct = default)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == periodId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Accounting period not found.");
        if (period.Status == AccountingPeriodStatus.Closed) return MapPeriod(period);
        await SyncAsync(period.StartsAt, period.EndsAt, ct);
        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedByUserId = userId;
        period.CloseNote = request.Note?.Trim();
        period.LastUpdatedAt = DateTime.UtcNow;
        period.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest("ACCOUNTING_PERIOD_CLOSED", "Finance", nameof(AccountingPeriod), period.Id.ToString(), NewValues: new { period.Name, period.ClosedAt, period.CloseNote }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return MapPeriod(period);
    }

    public async Task<AccountingPeriodDto> ReopenPeriodAsync(Guid userId, Guid periodId, CancellationToken ct = default)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == periodId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Accounting period not found.");
        period.Status = AccountingPeriodStatus.Open;
        period.ClosedAt = null; period.ClosedByUserId = null; period.CloseNote = null;
        period.LastUpdatedAt = DateTime.UtcNow; period.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest("ACCOUNTING_PERIOD_REOPENED", "Finance", nameof(AccountingPeriod), period.Id.ToString(), NewValues: new { period.Name }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return MapPeriod(period);
    }

    public async Task<JournalEntryDto> CreateManualJournalAsync(Guid userId, CreateManualJournalRequest request, CancellationToken ct = default)
    {
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var date = ToUtc(request.EntryDate == default ? DateTime.UtcNow : request.EntryDate);
        var tuples = request.Lines.Select(x => (x.DebitAmount, x.CreditAmount)).ToList();
        AccountingJournalValidator.Validate(tuples);
        var accountIds = request.Lines.Select(x => x.AccountingAccountId).Distinct().ToList();
        var accounts = await _db.AccountingAccounts.Where(x => accountIds.Contains(x.Id) && x.IsActive && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        if (accounts.Count != accountIds.Count) throw new InvalidOperationException("One or more accounting accounts are invalid or inactive.");
        var period = await GetOrCreatePeriodAsync(date, ct);
        EnsurePeriodOpen(period);
        var entry = new JournalEntry
        {
            Reference = NewReference("JE"), SourceKey = $"MANUAL:{Guid.NewGuid():N}", SourceType = AccountingSourceType.ManualAdjustment,
            AccountingPeriodId = period.Id, EntryDate = date, CurrencyCode = currency, Description = request.Description.Trim(), PostedByUserId = userId, CreatedByUserId = userId
        };
        foreach (var line in request.Lines)
            entry.Lines.Add(new JournalLine { AccountingAccountId = line.AccountingAccountId, DebitAmount = Round(line.DebitAmount), CreditAmount = Round(line.CreditAmount), Narrative = line.Narrative?.Trim() });
        _db.JournalEntries.Add(entry);
        _audit.Stage(new AuditRecordRequest("MANUAL_JOURNAL_POSTED", "Finance", nameof(JournalEntry), entry.Id.ToString(), NewValues: new { entry.Reference, entry.CurrencyCode, entry.Description, Lines = request.Lines.Count }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return await GetJournalDtoAsync(entry.Id, ct);
    }

    public async Task<JournalEntryDto> ReverseJournalAsync(Guid userId, Guid journalEntryId, ReverseJournalRequest request, CancellationToken ct = default)
    {
        var original = await _db.JournalEntries.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == journalEntryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Journal entry not found.");
        if (original.Status == JournalEntryStatus.Reversed)
            throw new InvalidOperationException("Journal entry has already been reversed.");
        if (await _db.JournalEntries.AnyAsync(x => x.ReversalOfJournalEntryId == original.Id && !x.IsDeleted, ct))
            throw new InvalidOperationException("A reversal journal already exists for this entry.");

        var date = ToUtc(request.EntryDate ?? DateTime.UtcNow);
        var period = await GetOrCreatePeriodAsync(date, ct);
        EnsurePeriodOpen(period);
        var reversal = new JournalEntry
        {
            Reference = NewReference("JRV"), SourceKey = $"REVERSAL:{original.Id}", SourceType = AccountingSourceType.JournalReversal, SourceId = original.Id,
            AccountingPeriodId = period.Id, EntryDate = date, CurrencyCode = original.CurrencyCode,
            Description = $"Reversal of {original.Reference}: {request.Reason.Trim()}", ReversalOfJournalEntryId = original.Id, PostedByUserId = userId, CreatedByUserId = userId
        };
        foreach (var line in original.Lines)
            reversal.Lines.Add(new JournalLine { AccountingAccountId = line.AccountingAccountId, DebitAmount = line.CreditAmount, CreditAmount = line.DebitAmount, Narrative = $"Reversal of {original.Reference}" });
        original.Status = JournalEntryStatus.Reversed;
        original.ReversedAt = DateTime.UtcNow; original.ReversedByUserId = userId; original.ReversalReason = request.Reason.Trim(); original.LastUpdatedAt = DateTime.UtcNow; original.LastUpdatedByUserId = userId;
        _db.JournalEntries.Add(reversal);
        _audit.Stage(new AuditRecordRequest("JOURNAL_REVERSED", "Finance", nameof(JournalEntry), original.Id.ToString(), NewValues: new { original.Reference, ReversalReference = reversal.Reference, request.Reason }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return await GetJournalDtoAsync(reversal.Id, ct);
    }

    public async Task<PagedResult<JournalEntryDto>> GetJournalsAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.JournalEntries.AsNoTracking().Where(x => !x.IsDeleted);
        if (from.HasValue) query = query.Where(x => x.EntryDate >= ToUtc(from.Value));
        if (to.HasValue) query = query.Where(x => x.EntryDate < ToUtc(to.Value));
        return await query.OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.CreatedAt)
            .Select(x => new JournalEntryDto
            {
                Id = x.Id, Reference = x.Reference, SourceKey = x.SourceKey, SourceType = x.SourceType, SourceId = x.SourceId,
                PeriodName = x.AccountingPeriod.Name, EntryDate = x.EntryDate, CurrencyCode = x.CurrencyCode, Description = x.Description, Status = x.Status,
                Lines = x.Lines.OrderBy(y => y.CreatedAt).Select(y => new JournalLineDto { Id = y.Id, AccountCode = y.AccountingAccount.Code, AccountName = y.AccountingAccount.Name, DebitAmount = y.DebitAmount, CreditAmount = y.CreditAmount, Narrative = y.Narrative }).ToList()
            }).PaginateAsync(page, pageSize, ct);
    }

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.JournalLines.AsNoTracking().Where(x => !x.IsDeleted && !x.JournalEntry.IsDeleted);
        if (from.HasValue) query = query.Where(x => x.JournalEntry.EntryDate >= ToUtc(from.Value));
        if (to.HasValue) query = query.Where(x => x.JournalEntry.EntryDate < ToUtc(to.Value));
        return await query.GroupBy(x => new { x.AccountingAccount.Code, x.AccountingAccount.Name, x.AccountingAccount.Type, x.JournalEntry.CurrencyCode })
            .Select(g => new TrialBalanceRowDto { AccountCode = g.Key.Code, AccountName = g.Key.Name, AccountType = g.Key.Type, CurrencyCode = g.Key.CurrencyCode, DebitTotal = g.Sum(x => x.DebitAmount), CreditTotal = g.Sum(x => x.CreditAmount), Balance = g.Sum(x => x.DebitAmount - x.CreditAmount) })
            .OrderBy(x => x.AccountCode).ThenBy(x => x.CurrencyCode).ToListAsync(ct);
    }

    public async Task<AccountingSyncResultDto> SyncAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        await EnsureSystemAccountsAsync(ct);
        var start = from.HasValue ? ToUtc(from.Value) : DateTime.UtcNow.AddDays(-31);
        var end = to.HasValue ? ToUtc(to.Value) : DateTime.UtcNow.AddMinutes(1);
        if (end <= start) throw new InvalidOperationException("Accounting sync end must be after start.");
        var result = new AccountingSyncResultDto();
        var accounts = await _db.AccountingAccounts.Where(x => x.IsSystem && x.IsActive && !x.IsDeleted).ToDictionaryAsync(x => x.Code, ct);

        var transfers = await _db.Transfers.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CompletedAt >= start && x.CompletedAt < end && (x.Status == TransferStatus.Completed || x.Status == TransferStatus.Refunded))
            .ToListAsync(ct);
        foreach (var transfer in transfers)
        {
            if (transfer.FeeAmount > 0)
                await TryPostAsync($"TRANSFER_FEE:{transfer.Id}", AccountingSourceType.TransferFee, transfer.Id, transfer.CompletedAt!.Value, transfer.FeeCurrencyCode, $"Transfer fee revenue for {transfer.Reference}", accounts[SettlementClearingCode], accounts[TransferFeeRevenueCode], transfer.FeeAmount, result, () => result.TransferFeeEntries++, ct);
            var spread = Round(Math.Max(0m, transfer.ProviderRate - transfer.CustomerRate) * transfer.SourceAmount);
            if (spread > 0)
                await TryPostAsync($"FX_SPREAD:{transfer.Id}", AccountingSourceType.FxSpread, transfer.Id, transfer.CompletedAt!.Value, transfer.DestinationCurrencyCode, $"FX spread revenue for {transfer.Reference}", accounts[SettlementClearingCode], accounts[FxSpreadRevenueCode], spread, result, () => result.FxSpreadEntries++, ct);
        }

        var refundedTransfers = await _db.Transfers.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == TransferStatus.Refunded && (x.LastUpdatedAt ?? x.CompletedAt) >= start && (x.LastUpdatedAt ?? x.CompletedAt) < end && x.CompletedAt != null)
            .ToListAsync(ct);
        foreach (var transfer in refundedTransfers)
        {
            if (transfer.FeeAmount > 0)
            {
                await TryPostAsync($"TRANSFER_FEE:{transfer.Id}", AccountingSourceType.TransferFee, transfer.Id, transfer.CompletedAt!.Value, transfer.FeeCurrencyCode, $"Transfer fee revenue for {transfer.Reference}", accounts[SettlementClearingCode], accounts[TransferFeeRevenueCode], transfer.FeeAmount, result, () => result.TransferFeeEntries++, ct);
                if (await JournalExistsAsync($"TRANSFER_FEE:{transfer.Id}", ct))
                    await TryPostAsync($"REFUND_FEE:{transfer.Id}", AccountingSourceType.RefundRevenueReversal, transfer.Id, transfer.LastUpdatedAt ?? DateTime.UtcNow, transfer.FeeCurrencyCode, $"Fee revenue reversal for refunded transfer {transfer.Reference}", accounts[TransferFeeRevenueCode], accounts[SettlementClearingCode], transfer.FeeAmount, result, () => result.RefundReversalEntries++, ct);
            }
            var spread = Round(Math.Max(0m, transfer.ProviderRate - transfer.CustomerRate) * transfer.SourceAmount);
            if (spread > 0)
            {
                await TryPostAsync($"FX_SPREAD:{transfer.Id}", AccountingSourceType.FxSpread, transfer.Id, transfer.CompletedAt!.Value, transfer.DestinationCurrencyCode, $"FX spread revenue for {transfer.Reference}", accounts[SettlementClearingCode], accounts[FxSpreadRevenueCode], spread, result, () => result.FxSpreadEntries++, ct);
                if (await JournalExistsAsync($"FX_SPREAD:{transfer.Id}", ct))
                    await TryPostAsync($"REFUND_SPREAD:{transfer.Id}", AccountingSourceType.RefundRevenueReversal, transfer.Id, transfer.LastUpdatedAt ?? DateTime.UtcNow, transfer.DestinationCurrencyCode, $"FX spread reversal for refunded transfer {transfer.Reference}", accounts[FxSpreadRevenueCode], accounts[SettlementClearingCode], spread, result, () => result.RefundReversalEntries++, ct);
            }
        }

        var providerFees = await _db.ProviderTransactions.AsNoTracking().Where(x => !x.IsDeleted && x.ProviderFeeAmount != null && x.ProviderFeeAmount > 0 && x.LastSyncedAt >= start && x.LastSyncedAt < end).ToListAsync(ct);
        foreach (var tx in providerFees)
            await TryPostAsync($"PROVIDER_FEE:{tx.Id}", AccountingSourceType.ProviderFee, tx.Id, tx.ProviderCreatedAt ?? tx.LastSyncedAt, tx.ProviderFeeCurrencyCode ?? tx.CurrencyCode, $"Provider processing fee for {tx.ProviderTransactionId}", accounts[ProviderFeeExpenseCode], accounts[SettlementClearingCode], tx.ProviderFeeAmount!.Value, result, () => result.ProviderFeeEntries++, ct);

        var settlements = await _db.SettlementBatches.AsNoTracking().Where(x => !x.IsDeleted && x.ReconciledAt >= start && x.ReconciledAt < end && x.VarianceAmount != null && x.VarianceAmount != 0).ToListAsync(ct);
        foreach (var batch in settlements)
        {
            var amount = Math.Abs(batch.VarianceAmount!.Value);
            var debit = batch.VarianceAmount > 0 ? accounts[SettlementClearingCode] : accounts[SettlementVarianceExpenseCode];
            var credit = batch.VarianceAmount > 0 ? accounts[SettlementVarianceGainCode] : accounts[SettlementClearingCode];
            await TryPostAsync($"SETTLEMENT_VARIANCE:{batch.Id}", AccountingSourceType.SettlementVariance, batch.Id, batch.ReconciledAt!.Value, batch.CurrencyCode, $"Settlement variance for {batch.Reference}", debit, credit, amount, result, () => result.SettlementVarianceEntries++, ct);
        }
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<byte[]> ExportJournalsCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var rows = await GetTrialBalanceAsync(from, to, ct);
        var sb = new StringBuilder("account_code,account_name,account_type,currency,debit_total,credit_total,balance\n");
        foreach (var x in rows) sb.AppendLine($"{Csv(x.AccountCode)},{Csv(x.AccountName)},{x.AccountType},{Csv(x.CurrencyCode)},{x.DebitTotal:0.00},{x.CreditTotal:0.00},{x.Balance:0.00}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<bool> JournalExistsAsync(string sourceKey, CancellationToken ct)
    {
        if (_db.ChangeTracker.Entries<JournalEntry>().Any(x => x.State == EntityState.Added && x.Entity.SourceKey == sourceKey))
            return true;

        return await _db.JournalEntries.AsNoTracking()
            .AnyAsync(x => x.SourceKey == sourceKey && !x.IsDeleted, ct);
    }

    private async Task TryPostAsync(string sourceKey, AccountingSourceType sourceType, Guid sourceId, DateTime date, string currency, string description, AccountingAccount debit, AccountingAccount credit, decimal amount, AccountingSyncResultDto result, Action increment, CancellationToken ct)
    {
        if (await _db.JournalEntries.AnyAsync(x => x.SourceKey == sourceKey, ct)) { result.AlreadyPosted++; return; }
        var period = await GetOrCreatePeriodAsync(date, ct);
        if (period.Status == AccountingPeriodStatus.Closed) { result.SkippedClosedPeriod++; return; }
        amount = Round(amount); if (amount <= 0) return;
        var entry = new JournalEntry { Reference = NewReference("JE"), SourceKey = sourceKey, SourceType = sourceType, SourceId = sourceId, AccountingPeriodId = period.Id, EntryDate = ToUtc(date), CurrencyCode = currency.Trim().ToUpperInvariant(), Description = description };
        entry.Lines.Add(new JournalLine { AccountingAccountId = debit.Id, DebitAmount = amount, CreditAmount = 0m, Narrative = description });
        entry.Lines.Add(new JournalLine { AccountingAccountId = credit.Id, DebitAmount = 0m, CreditAmount = amount, Narrative = description });
        _db.JournalEntries.Add(entry); increment();
    }

    private async Task EnsureSystemAccountsAsync(CancellationToken ct)
    {
        var definitions = new[]
        {
            (SettlementClearingCode, "Provider Settlement Clearing", AccountingAccountType.Asset, "Clearing account for amounts due from or deducted by providers."),
            (TransferFeeRevenueCode, "Transfer Fee Revenue", AccountingAccountType.Revenue, "Customer transfer fee revenue."),
            (FxSpreadRevenueCode, "FX Spread Revenue", AccountingAccountType.Revenue, "Realized transfer FX spread revenue."),
            (SettlementVarianceGainCode, "Settlement Variance Gain", AccountingAccountType.Revenue, "Positive settlement reconciliation variances."),
            (ProviderFeeExpenseCode, "Provider Processing Expense", AccountingAccountType.Expense, "Provider collection, payout, refund and processing fees."),
            (SettlementVarianceExpenseCode, "Settlement Variance Expense", AccountingAccountType.Expense, "Negative settlement reconciliation variances."),
            (ProviderFeeAdjustmentGainCode, "Provider Fee Adjustment Gain", AccountingAccountType.Revenue, "Favorable provider invoice fee reconciliation adjustments."),
            (IndirectTaxExpenseCode, "Indirect Tax Expense", AccountingAccountType.Expense, "VAT, GST and other indirect taxes charged by providers.")
        };
        var existing = await _db.AccountingAccounts.Where(x => definitions.Select(y => y.Item1).Contains(x.Code)).Select(x => x.Code).ToListAsync(ct);
        var added = false;
        foreach (var d in definitions.Where(x => !existing.Contains(x.Item1)))
        {
            _db.AccountingAccounts.Add(new AccountingAccount { Code = d.Item1, Name = d.Item2, Type = d.Item3, Description = d.Item4, IsSystem = true, IsActive = true });
            added = true;
        }
        if (added) await _db.SaveChangesAsync(ct);
    }

    private async Task<AccountingPeriod> GetOrCreatePeriodAsync(DateTime date, CancellationToken ct)
    {
        date = ToUtc(date);
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(x => !x.IsDeleted && x.StartsAt <= date && x.EndsAt > date, ct);
        if (period is not null) return period;
        var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        period = new AccountingPeriod { Name = start.ToString("yyyy-MM"), StartsAt = start, EndsAt = end };
        _db.AccountingPeriods.Add(period);
        await _db.SaveChangesAsync(ct);
        return period;
    }

    private static void EnsurePeriodOpen(AccountingPeriod p) { if (p.Status != AccountingPeriodStatus.Open) throw new InvalidOperationException($"Accounting period '{p.Name}' is closed."); }
    private async Task<JournalEntryDto> GetJournalDtoAsync(Guid id, CancellationToken ct) =>
        await _db.JournalEntries.AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new JournalEntryDto
            {
                Id = x.Id, Reference = x.Reference, SourceKey = x.SourceKey, SourceType = x.SourceType, SourceId = x.SourceId,
                PeriodName = x.AccountingPeriod.Name, EntryDate = x.EntryDate, CurrencyCode = x.CurrencyCode, Description = x.Description, Status = x.Status,
                Lines = x.Lines.OrderBy(y => y.CreatedAt).Select(y => new JournalLineDto
                {
                    Id = y.Id, AccountCode = y.AccountingAccount.Code, AccountName = y.AccountingAccount.Name,
                    DebitAmount = y.DebitAmount, CreditAmount = y.CreditAmount, Narrative = y.Narrative
                }).ToList()
            }).FirstAsync(ct);
    private static AccountingAccountDto MapAccount(AccountingAccount x) => new() { Id = x.Id, Code = x.Code, Name = x.Name, Type = x.Type, IsSystem = x.IsSystem, IsActive = x.IsActive, Description = x.Description };
    private static AccountingPeriodDto MapPeriod(AccountingPeriod x) => new() { Id = x.Id, Name = x.Name, StartsAt = x.StartsAt, EndsAt = x.EndsAt, Status = x.Status, ClosedAt = x.ClosedAt, ClosedByUserId = x.ClosedByUserId, CloseNote = x.CloseNote };
    private static decimal Round(decimal x) => decimal.Round(x, 2, MidpointRounding.AwayFromZero);
    private static DateTime ToUtc(DateTime x) => x.Kind == DateTimeKind.Utc ? x : x.ToUniversalTime();
    private static string NewReference(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    private static string Csv(string x) => x.Contains(',') || x.Contains('"') ? '"' + x.Replace("\"", "\"\"") + '"' : x;
}
