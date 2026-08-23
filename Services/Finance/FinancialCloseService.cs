using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Finance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Finance;
using KorridorX.Models.Providers;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KorridorX.Services.Finance;

public sealed class FinancialCloseService : IFinancialCloseService
{
    private const decimal InvoiceVarianceTolerance = 0.01m;
    private const string PendingCloseRequestUniqueIndex = "IX_FinanceCloseRequests_AccountingPeriodId_Pending";
    private const string SettlementClearingCode = "1100";
    private const string ProviderFeeExpenseCode = "5000";
    private const string ProviderFeeAdjustmentGainCode = "4030";
    private const string IndirectTaxExpenseCode = "5030";

    private readonly AppDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly IAuditService _audit;
    private readonly AccountingOptions _options;

    public FinancialCloseService(AppDbContext db, IAccountingService accounting, IAuditService audit, IOptions<AccountingOptions> options)
    {
        _db = db;
        _accounting = accounting;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<FinanceTranslationRateDto>> GetTranslationRatesAsync(string? sourceCurrencyCode = null, string? baseCurrencyCode = null, CancellationToken ct = default)
    {
        var query = _db.FinanceTranslationRates.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(sourceCurrencyCode)) { var code = Normalize(sourceCurrencyCode); query = query.Where(x => x.SourceCurrencyCode == code); }
        if (!string.IsNullOrWhiteSpace(baseCurrencyCode)) { var code = Normalize(baseCurrencyCode); query = query.Where(x => x.BaseCurrencyCode == code); }
        var rows = await query.OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.RateType).ThenByDescending(x => x.EffectiveFrom).ToListAsync(ct);
        return rows.Select(MapRate).ToList();
    }

    public async Task<FinanceTranslationRateDto> UpsertTranslationRateAsync(Guid userId, UpsertFinanceTranslationRateRequest request, CancellationToken ct = default)
    {
        var source = Normalize(request.SourceCurrencyCode); var baseCode = Normalize(request.BaseCurrencyCode);
        if (source == baseCode && request.Rate != 1m) throw new InvalidOperationException("A currency translated to itself must use a rate of 1.");
        var effectiveFrom = ToUtc(request.EffectiveFrom == default ? DateTime.UtcNow : request.EffectiveFrom);
        DateTime? effectiveTo = request.EffectiveTo.HasValue ? ToUtc(request.EffectiveTo.Value) : null;
        if (effectiveTo.HasValue && effectiveTo <= effectiveFrom) throw new InvalidOperationException("Translation-rate end must be after its start.");

        var entity = new FinanceTranslationRate
        {
            SourceCurrencyCode = source, BaseCurrencyCode = baseCode, RateType = request.RateType, Rate = request.Rate,
            EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo, Source = request.Source.Trim(), IsActive = true, CreatedByUserId = userId
        };
        _db.FinanceTranslationRates.Add(entity);
        _audit.Stage(new AuditRecordRequest("FINANCE_TRANSLATION_RATE_CREATED", "Finance", nameof(FinanceTranslationRate), entity.Id.ToString(), NewValues: new { source, baseCode, request.RateType, request.Rate, effectiveFrom, effectiveTo }, UserId: userId));
        await _db.SaveChangesAsync(ct);
        return MapRate(entity);
    }

    public async Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime from, DateTime to, string? baseCurrencyCode = null, CancellationToken ct = default)
    {
        var start = ToUtc(from); var end = ToUtc(to); if (end <= start) throw new InvalidOperationException("Statement end must be after start.");
        var baseCode = Normalize(string.IsNullOrWhiteSpace(baseCurrencyCode) ? _options.BaseCurrencyCode : baseCurrencyCode);
        var data = await _db.JournalLines.AsNoTracking().Where(x => !x.IsDeleted && !x.JournalEntry.IsDeleted && x.JournalEntry.EntryDate >= start && x.JournalEntry.EntryDate < end && (x.AccountingAccount.Type == AccountingAccountType.Revenue || x.AccountingAccount.Type == AccountingAccountType.Expense))
            .Select(x => new { x.AccountingAccount.Code, x.AccountingAccount.Name, x.AccountingAccount.Type, x.JournalEntry.CurrencyCode, x.DebitAmount, x.CreditAmount }).ToListAsync(ct);
        var grouped = data.GroupBy(x => new { x.Code, x.Name, x.Type, x.CurrencyCode })
            .Select(g => new StatementRow(g.Key.Code, g.Key.Name, g.Key.Type, g.Key.CurrencyCode, g.Key.Type == AccountingAccountType.Revenue ? g.Sum(x => x.CreditAmount - x.DebitAmount) : g.Sum(x => x.DebitAmount - x.CreditAmount)))
            .Where(x => x.Amount != 0).ToList();
        var rates = await ResolveRatesAsync(grouped.Select(x => x.CurrencyCode), baseCode, FinanceTranslationRateType.PeriodAverage, end.AddTicks(-1), ct);
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var revenue = grouped.Where(x => x.AccountType == AccountingAccountType.Revenue).Select(x => StatementLine(x.AccountCode, x.AccountName, x.AccountType, x.CurrencyCode, x.Amount, rates, missing)).OrderBy(x => x.AccountCode).ThenBy(x => x.CurrencyCode).ToList();
        var expenses = grouped.Where(x => x.AccountType == AccountingAccountType.Expense).Select(x => StatementLine(x.AccountCode, x.AccountName, x.AccountType, x.CurrencyCode, x.Amount, rates, missing)).OrderBy(x => x.AccountCode).ThenBy(x => x.CurrencyCode).ToList();
        var complete = missing.Count == 0;
        decimal? rev = complete ? revenue.Sum(x => x.BaseAmount ?? 0m) : null; decimal? exp = complete ? expenses.Sum(x => x.BaseAmount ?? 0m) : null;
        return new IncomeStatementDto { From = start, To = end, BaseCurrencyCode = baseCode, Revenue = revenue, Expenses = expenses, BaseRevenueTotal = rev, BaseExpenseTotal = exp, BaseNetIncome = complete ? rev - exp : null, MissingTranslationCurrencies = missing.OrderBy(x => x).ToList() };
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOf, string? baseCurrencyCode = null, CancellationToken ct = default)
    {
        var at = ToUtc(asOf); var baseCode = Normalize(string.IsNullOrWhiteSpace(baseCurrencyCode) ? _options.BaseCurrencyCode : baseCurrencyCode);
        var data = await _db.JournalLines.AsNoTracking().Where(x => !x.IsDeleted && !x.JournalEntry.IsDeleted && x.JournalEntry.EntryDate <= at)
            .Select(x => new { x.AccountingAccount.Code, x.AccountingAccount.Name, x.AccountingAccount.Type, x.JournalEntry.CurrencyCode, x.DebitAmount, x.CreditAmount }).ToListAsync(ct);
        var balanceTypes = new[] { AccountingAccountType.Asset, AccountingAccountType.Liability, AccountingAccountType.Equity };
        var grouped = data.Where(x => balanceTypes.Contains(x.Type)).GroupBy(x => new { x.Code, x.Name, x.Type, x.CurrencyCode })
            .Select(g => new StatementRow(g.Key.Code, g.Key.Name, g.Key.Type, g.Key.CurrencyCode, g.Key.Type == AccountingAccountType.Asset ? g.Sum(x => x.DebitAmount - x.CreditAmount) : g.Sum(x => x.CreditAmount - x.DebitAmount)))
            .Where(x => x.Amount != 0).ToList();
        var earnings = data.Where(x => x.Type is AccountingAccountType.Revenue or AccountingAccountType.Expense).GroupBy(x => x.CurrencyCode)
            .Select(g => new CurrencyAmountRow(g.Key, g.Where(x => x.Type == AccountingAccountType.Revenue).Sum(x => x.CreditAmount - x.DebitAmount) - g.Where(x => x.Type == AccountingAccountType.Expense).Sum(x => x.DebitAmount - x.CreditAmount)))
            .Where(x => x.Amount != 0).ToList();
        var currencies = grouped.Select(x => x.CurrencyCode).Concat(earnings.Select(x => x.CurrencyCode));
        var rates = await ResolveRatesAsync(currencies, baseCode, FinanceTranslationRateType.Closing, at, ct); var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assets = grouped.Where(x => x.AccountType == AccountingAccountType.Asset).Select(x => StatementLine(x.AccountCode, x.AccountName, x.AccountType, x.CurrencyCode, x.Amount, rates, missing)).OrderBy(x => x.AccountCode).ToList();
        var liabilities = grouped.Where(x => x.AccountType == AccountingAccountType.Liability).Select(x => StatementLine(x.AccountCode, x.AccountName, x.AccountType, x.CurrencyCode, x.Amount, rates, missing)).OrderBy(x => x.AccountCode).ToList();
        var equity = grouped.Where(x => x.AccountType == AccountingAccountType.Equity).Select(x => StatementLine(x.AccountCode, x.AccountName, x.AccountType, x.CurrencyCode, x.Amount, rates, missing)).OrderBy(x => x.AccountCode).ToList();
        foreach (var e in earnings) equity.Add(StatementLine("3999", "Current Earnings", AccountingAccountType.Equity, e.CurrencyCode, e.Amount, rates, missing, true));
        var complete = missing.Count == 0;
        decimal? assetTotal = complete ? assets.Sum(x => x.BaseAmount ?? 0m) : null;
        decimal? leTotal = complete ? liabilities.Concat(equity).Sum(x => x.BaseAmount ?? 0m) : null;
        return new BalanceSheetDto { AsOf = at, BaseCurrencyCode = baseCode, Assets = assets, Liabilities = liabilities, Equity = equity.OrderBy(x => x.AccountCode).ThenBy(x => x.CurrencyCode).ToList(), BaseAssetsTotal = assetTotal, BaseLiabilitiesAndEquityTotal = leTotal, BaseDifference = complete ? assetTotal - leTotal : null, MissingTranslationCurrencies = missing.OrderBy(x => x).ToList() };
    }

    public async Task<PagedResult<ProviderInvoiceDto>> GetProviderInvoicesAsync(ProviderInvoiceStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ProviderInvoices.AsNoTracking().Where(x => !x.IsDeleted); if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        var paged = await q.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt).PaginateAsync(page, pageSize, ct);
        return new PagedResult<ProviderInvoiceDto> { Meta = paged.Meta, Items = paged.Items.Select(x => MapInvoice(x, new List<ProviderInvoiceLine>())).ToList() };
    }

    public async Task<ProviderInvoiceDto> GetProviderInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var x = await _db.ProviderInvoices.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Provider invoice not found.");
        return MapInvoice(x, x.Lines.OrderBy(y => y.RowNumber).ToList());
    }

    public async Task<ProviderInvoiceDto> ImportProviderInvoiceAsync(Guid userId, ImportProviderInvoiceFormDto request, CancellationToken ct = default)
    {
        if (request.File is null || request.File.Length == 0) throw new InvalidOperationException("A provider invoice CSV file is required.");
        if (request.File.Length > 10 * 1024 * 1024) throw new InvalidOperationException("Provider invoice file cannot exceed 10 MB.");
        if (!Path.GetExtension(request.File.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Provider invoice must be a CSV file.");
        var invoiceDate = ToUtc(request.InvoiceDate); var start = ToUtc(request.PeriodStart); var end = ToUtc(request.PeriodEnd);
        if (end <= start) throw new InvalidOperationException("Provider invoice period end must be after its start.");
        var currency = Normalize(request.CurrencyCode); var invoiceNumber = request.InvoiceNumber.Trim();
        if (await _db.ProviderInvoices.AnyAsync(x => x.ProviderCode == request.ProviderCode && x.InvoiceNumber == invoiceNumber && !x.IsDeleted, ct)) throw new InvalidOperationException("This provider invoice number has already been imported.");
        await using var ms = new MemoryStream(); await request.File.CopyToAsync(ms, ct); var bytes = ms.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (await _db.ProviderInvoices.AnyAsync(x => x.ProviderCode == request.ProviderCode && x.FileHash == hash && !x.IsDeleted, ct)) throw new InvalidOperationException("This provider invoice file has already been imported.");
        ms.Position = 0; using var reader = new StreamReader(ms, Encoding.UTF8, true, 1024, true);
        var headers = ParseHeaders(await reader.ReadLineAsync(ct) ?? throw new InvalidOperationException("Provider invoice is empty."));
        foreach (var required in new[] { "description", "net_amount", "tax_amount", "total_amount" }) if (!headers.ContainsKey(required)) throw new InvalidOperationException($"Provider invoice is missing required column '{required}'.");
        var providerTxs = await _db.ProviderTransactions.AsNoTracking().Where(x => !x.IsDeleted && x.ProviderCode == request.ProviderCode).ToListAsync(ct);
        var byId = providerTxs.Where(x => !string.IsNullOrWhiteSpace(x.ProviderTransactionId)).GroupBy(x => x.ProviderTransactionId, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastSyncedAt).First(), StringComparer.OrdinalIgnoreCase);
        var byRef = providerTxs.Where(x => !string.IsNullOrWhiteSpace(x.ProviderReference)).GroupBy(x => x.ProviderReference!, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastSyncedAt).First(), StringComparer.OrdinalIgnoreCase);
        var invoice = new ProviderInvoice { ProviderCode = request.ProviderCode, InvoiceNumber = invoiceNumber, InvoiceDate = invoiceDate, PeriodStart = start, PeriodEnd = end, CurrencyCode = currency, FileName = Path.GetFileName(request.File.FileName), FileHash = hash, Note = request.Note?.Trim(), ImportedByUserId = userId, CreatedByUserId = userId };
        var row = 1;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            row++; if (string.IsNullOrWhiteSpace(line)) continue; var values = ParseCsvLine(line);
            string Get(string name) => headers[name] < values.Count ? values[headers[name]].Trim() : "";
            string? Opt(string name) => headers.TryGetValue(name, out var i) && i < values.Count && !string.IsNullOrWhiteSpace(values[i]) ? values[i].Trim() : null;
            decimal Money(string name) => decimal.TryParse(Get(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v >= 0 ? decimal.Round(v, 2, MidpointRounding.AwayFromZero) : throw new InvalidOperationException($"Row {row}: {name} is invalid.");
            var net = Money("net_amount"); var tax = Money("tax_amount"); var total = Money("total_amount"); if (Math.Abs((net + tax) - total) > 0.01m) throw new InvalidOperationException($"Row {row}: total_amount must equal net_amount + tax_amount.");
            var txid = Opt("provider_transaction_id"); var reference = Opt("provider_reference"); ProviderTransaction? matched = null;
            if (!string.IsNullOrWhiteSpace(txid)) byId.TryGetValue(txid, out matched); if (matched is null && !string.IsNullOrWhiteSpace(reference)) byRef.TryGetValue(reference, out matched);
            var recordedFee = matched?.ProviderFeeAmount ?? 0m;
            invoice.Lines.Add(new ProviderInvoiceLine { RowNumber = row, ProviderTransactionId = txid, ProviderReference = reference, Description = Get("description"), NetAmount = net, TaxAmount = tax, TotalAmount = total, IsMatched = matched is not null, ProviderTransactionRowId = matched?.Id, MatchedProviderFeeAmount = recordedFee, VarianceAmount = decimal.Round(net - recordedFee, 2, MidpointRounding.AwayFromZero) });
        }
        if (invoice.Lines.Count == 0) throw new InvalidOperationException("Provider invoice contains no rows.");
        invoice.NetAmount = invoice.Lines.Sum(x => x.NetAmount); invoice.TaxAmount = invoice.Lines.Sum(x => x.TaxAmount); invoice.TotalAmount = invoice.Lines.Sum(x => x.TotalAmount); invoice.MatchedProviderFeeAmount = invoice.Lines.Sum(x => x.MatchedProviderFeeAmount); invoice.VarianceAmount = invoice.Lines.Sum(x => x.VarianceAmount);
        invoice.Status = invoice.Lines.All(x => x.IsMatched) && Math.Abs(invoice.VarianceAmount) <= InvoiceVarianceTolerance ? ProviderInvoiceStatus.Matched : ProviderInvoiceStatus.Variance;
        _db.ProviderInvoices.Add(invoice); _audit.Stage(new AuditRecordRequest("PROVIDER_INVOICE_IMPORTED", "Finance", nameof(ProviderInvoice), invoice.Id.ToString(), NewValues: new { invoice.ProviderCode, invoice.InvoiceNumber, invoice.CurrencyCode, invoice.TotalAmount, invoice.VarianceAmount, invoice.Status, Rows = invoice.Lines.Count }, UserId: userId));
        await _db.SaveChangesAsync(ct); return await GetProviderInvoiceAsync(invoice.Id, ct);
    }

    public async Task<ProviderInvoiceDto> ReviewProviderInvoiceAsync(Guid userId, Guid invoiceId, ReviewProviderInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await _db.ProviderInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Provider invoice not found.");
        if (invoice.Status is ProviderInvoiceStatus.Posted or ProviderInvoiceStatus.Rejected) throw new InvalidOperationException("Provider invoice has already reached a terminal review state.");
        if (request.Approve && invoice.ImportedByUserId == userId) throw new InvalidOperationException("The user who imported a provider invoice cannot approve it.");
        invoice.ReviewedByUserId = userId; invoice.ReviewedAt = DateTime.UtcNow; invoice.ReviewNote = request.Note.Trim(); invoice.LastUpdatedAt = DateTime.UtcNow; invoice.LastUpdatedByUserId = userId;
        if (!request.Approve) invoice.Status = ProviderInvoiceStatus.Rejected;
        else { invoice.Status = ProviderInvoiceStatus.Approved; await PostProviderInvoiceAsync(invoice, userId, ct); }
        _audit.Stage(new AuditRecordRequest(request.Approve ? "PROVIDER_INVOICE_APPROVED" : "PROVIDER_INVOICE_REJECTED", "Finance", nameof(ProviderInvoice), invoice.Id.ToString(), NewValues: new { invoice.Status, invoice.ReviewNote, invoice.VarianceAmount, invoice.TaxAmount }, UserId: userId));
        await _db.SaveChangesAsync(ct); return await GetProviderInvoiceAsync(invoice.Id, ct);
    }

    public async Task<IReadOnlyList<TaxRuleDto>> GetTaxRulesAsync(CancellationToken ct = default)
    {
        var rows = await _db.TaxRules.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.JurisdictionCode).ThenBy(x => x.TaxCode).ToListAsync(ct);
        return rows.Select(MapTaxRule).ToList();
    }

    public async Task<TaxRuleDto> UpsertTaxRuleAsync(Guid userId, Guid? taxRuleId, UpsertTaxRuleRequest request, CancellationToken ct = default)
    {
        TaxRule entity;
        if (taxRuleId.HasValue) entity = await _db.TaxRules.FirstOrDefaultAsync(x => x.Id == taxRuleId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Tax rule not found.");
        else { entity = new TaxRule { CreatedByUserId = userId }; _db.TaxRules.Add(entity); }
        var start = ToUtc(request.EffectiveFrom == default ? DateTime.UtcNow : request.EffectiveFrom); DateTime? end = request.EffectiveTo.HasValue ? ToUtc(request.EffectiveTo.Value) : null; if (end.HasValue && end <= start) throw new InvalidOperationException("Tax-rule end must be after its start.");
        entity.JurisdictionCode = Normalize(request.JurisdictionCode); entity.TaxCode = request.TaxCode.Trim().ToUpperInvariant(); entity.Name = request.Name.Trim(); entity.TaxType = request.TaxType; entity.AppliesTo = request.AppliesTo; entity.RatePercentage = request.RatePercentage; entity.IsInclusive = request.IsInclusive; entity.IsRecoverable = request.IsRecoverable; entity.EffectiveFrom = start; entity.EffectiveTo = end; entity.IsActive = request.IsActive; entity.LastUpdatedAt = DateTime.UtcNow; entity.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest("TAX_RULE_UPSERTED", "Finance", nameof(TaxRule), entity.Id.ToString(), NewValues: new { entity.JurisdictionCode, entity.TaxCode, entity.TaxType, entity.AppliesTo, entity.RatePercentage, entity.IsInclusive, entity.IsRecoverable, entity.IsActive }, UserId: userId)); await _db.SaveChangesAsync(ct); return MapTaxRule(entity);
    }

    public async Task<TaxCalculationDto> CalculateTaxAsync(CalculateTaxRequest request, CancellationToken ct = default)
    {
        var at = ToUtc(request.At ?? DateTime.UtcNow); var jurisdiction = Normalize(request.JurisdictionCode); var rules = await _db.TaxRules.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive && x.JurisdictionCode == jurisdiction && x.AppliesTo == request.AppliesTo && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at)).OrderBy(x => x.TaxCode).ToListAsync(ct);
        var result = new TaxCalculationDto { InputAmount = request.Amount, AmountExcludingTax = request.Amount, AmountIncludingTax = request.Amount };
        foreach (var r in rules)
        {
            var rate = r.RatePercentage / 100m; var tax = r.IsInclusive ? request.Amount - (request.Amount / (1m + rate)) : request.Amount * rate; tax = decimal.Round(tax, 2, MidpointRounding.AwayFromZero);
            result.Taxes.Add(new TaxCalculationLineDto { TaxCode = r.TaxCode, Name = r.Name, RatePercentage = r.RatePercentage, IsInclusive = r.IsInclusive, TaxAmount = tax }); result.TaxAmount += tax;
            if (r.IsInclusive) result.AmountExcludingTax -= tax; else result.AmountIncludingTax += tax;
        }
        result.TaxAmount = decimal.Round(result.TaxAmount, 2, MidpointRounding.AwayFromZero); result.AmountExcludingTax = decimal.Round(result.AmountExcludingTax, 2, MidpointRounding.AwayFromZero); result.AmountIncludingTax = decimal.Round(result.AmountIncludingTax, 2, MidpointRounding.AwayFromZero); return result;
    }

    public async Task<AccrualResultDto> CreateAccrualAsync(Guid userId, CreateAccrualRequest request, CancellationToken ct = default)
    {
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) throw new InvalidOperationException("Accrual amount must be greater than zero.");
        var entryDate = ToUtc(request.EntryDate == default ? DateTime.UtcNow : request.EntryDate);
        DateTime? reversalDate = request.ReversalDate.HasValue ? ToUtc(request.ReversalDate.Value) : null;
        if (reversalDate.HasValue && reversalDate <= entryDate) throw new InvalidOperationException("Accrual reversal date must be after the accrual date.");
        var accountIds = new[] { request.ExpenseAccountId, request.LiabilityAccountId };
        var accounts = await _db.AccountingAccounts.Where(x => accountIds.Contains(x.Id) && x.IsActive && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        if (accounts.Count != 2) throw new InvalidOperationException("Accrual expense and liability accounts must be active accounting accounts.");
        if (accounts[request.ExpenseAccountId].Type != AccountingAccountType.Expense) throw new InvalidOperationException("Accrual expense account must be an Expense account.");
        if (accounts[request.LiabilityAccountId].Type != AccountingAccountType.Liability) throw new InvalidOperationException("Accrual liability account must be a Liability account.");
        var period = await GetOrCreatePeriodForDateAsync(entryDate, ct); if (period.Status != AccountingPeriodStatus.Open) throw new InvalidOperationException($"Accounting period '{period.Name}' is closed.");
        var sourceId = Guid.NewGuid(); var description = request.Description.Trim(); var currency = Normalize(request.CurrencyCode);
        var journal = new JournalEntry { Reference = NewReference("JAC"), SourceKey = $"ACCRUAL:{sourceId}", SourceType = AccountingSourceType.Accrual, SourceId = sourceId, AccountingPeriodId = period.Id, EntryDate = entryDate, CurrencyCode = currency, Description = description, PostedByUserId = userId, CreatedByUserId = userId };
        journal.Lines.Add(Line(accounts[request.ExpenseAccountId], amount, 0m, description)); journal.Lines.Add(Line(accounts[request.LiabilityAccountId], 0m, amount, description)); _db.JournalEntries.Add(journal);
        JournalEntry? reversal = null;
        if (reversalDate.HasValue)
        {
            var reversalPeriod = await GetOrCreatePeriodForDateAsync(reversalDate.Value, ct); if (reversalPeriod.Status != AccountingPeriodStatus.Open) throw new InvalidOperationException($"Accrual reversal period '{reversalPeriod.Name}' is closed.");
            reversal = new JournalEntry { Reference = NewReference("JAR"), SourceKey = $"ACCRUAL_REVERSAL:{sourceId}", SourceType = AccountingSourceType.AccrualReversal, SourceId = sourceId, AccountingPeriodId = reversalPeriod.Id, EntryDate = reversalDate.Value, CurrencyCode = currency, Description = $"Reversal: {description}", ReversalOfJournalEntryId = journal.Id, PostedByUserId = userId, CreatedByUserId = userId };
            reversal.Lines.Add(Line(accounts[request.LiabilityAccountId], amount, 0m, $"Reversal: {description}")); reversal.Lines.Add(Line(accounts[request.ExpenseAccountId], 0m, amount, $"Reversal: {description}")); _db.JournalEntries.Add(reversal);
        }
        _audit.Stage(new AuditRecordRequest("ACCRUAL_POSTED", "Finance", nameof(JournalEntry), journal.Id.ToString(), NewValues: new { journal.Reference, currency, amount, entryDate, reversalDate, description }, UserId: userId)); await _db.SaveChangesAsync(ct);
        return new AccrualResultDto { AccrualJournal = await LoadJournalAsync(journal.Id, ct), ReversalJournal = reversal is null ? null : await LoadJournalAsync(reversal.Id, ct) };
    }

    public async Task<IReadOnlyList<FinanceCloseChecklistItemDto>> GetCloseChecklistAsync(Guid periodId, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct); await EnsureChecklistAsync(period, ct); await RefreshSystemChecklistAsync(period, ct); await _db.SaveChangesAsync(ct);
        var rows = await _db.FinanceCloseChecklistItems.AsNoTracking().Where(x => x.AccountingPeriodId == periodId && !x.IsDeleted).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return rows.Select(MapChecklist).ToList();
    }

    public async Task<FinanceCloseChecklistItemDto> UpdateChecklistItemAsync(Guid userId, Guid periodId, Guid itemId, UpdateFinanceCloseChecklistItemRequest request, CancellationToken ct = default)
    {
        var item = await _db.FinanceCloseChecklistItems.FirstOrDefaultAsync(x => x.Id == itemId && x.AccountingPeriodId == periodId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Finance close checklist item not found.");
        if (item.IsSystemCheck) throw new InvalidOperationException("System finance-close checks cannot be completed manually.");
        item.IsCompleted = request.IsCompleted; item.CompletedAt = request.IsCompleted ? DateTime.UtcNow : null; item.CompletedByUserId = request.IsCompleted ? userId : null; item.Note = request.Note?.Trim(); item.LastUpdatedAt = DateTime.UtcNow; item.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest("FINANCE_CLOSE_CHECKLIST_UPDATED", "Finance", nameof(FinanceCloseChecklistItem), item.Id.ToString(), NewValues: new { item.Code, item.IsCompleted, item.Note }, UserId: userId)); await _db.SaveChangesAsync(ct); return MapChecklist(item);
    }

    public async Task<PagedResult<FinanceCloseRequestDto>> GetCloseRequestsAsync(Guid? periodId, FinanceCloseRequestStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.FinanceCloseRequests.AsNoTracking().Include(x => x.AccountingPeriod).Where(x => !x.IsDeleted);
        if (periodId.HasValue) query = query.Where(x => x.AccountingPeriodId == periodId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var paged = await query.OrderByDescending(x => x.RequestedAt).PaginateAsync(page, pageSize, ct);
        return new PagedResult<FinanceCloseRequestDto> { Meta = paged.Meta, Items = paged.Items.Select(x => MapCloseRequest(x, x.AccountingPeriod.Name)).ToList() };
    }

    public async Task<FinanceCloseRequestDto> SubmitCloseRequestAsync(Guid userId, Guid periodId, CreateFinanceCloseRequest request, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct);
        if (period.Status == AccountingPeriodStatus.Closed)
            throw new InvalidOperationException("Accounting period is already closed.");

        await _accounting.SyncAsync(period.StartsAt, period.EndsAt, ct);

        var checklist = await GetCloseChecklistAsync(periodId, ct);
        var incomplete = checklist
            .Where(x => x.IsRequired && !x.IsCompleted)
            .Select(x => x.Label)
            .ToList();

        if (incomplete.Count > 0)
            throw new InvalidOperationException(
                "Finance close checklist is incomplete: " +
                string.Join(", ", incomplete));

        if (await _db.FinanceCloseRequests.AnyAsync(
                x => x.AccountingPeriodId == periodId &&
                     x.Status == FinanceCloseRequestStatus.PendingApproval &&
                     !x.IsDeleted,
                ct))
        {
            throw new InvalidOperationException(
                "A finance close request is already pending approval.");
        }

        var entity = new FinanceCloseRequest
        {
            AccountingPeriodId = periodId,
            RequestedByUserId = userId,
            RequestedAt = DateTime.UtcNow,
            RequestNote = request.Note?.Trim(),
            CreatedByUserId = userId
        };

        _db.FinanceCloseRequests.Add(entity);

        _audit.Stage(
            new AuditRecordRequest(
                "FINANCE_CLOSE_REQUESTED",
                "Finance",
                nameof(FinanceCloseRequest),
                entity.Id.ToString(),
                NewValues: new { period.Name, entity.RequestNote },
                UserId: userId));

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsPendingCloseUniqueViolation(ex))
        {
            throw new InvalidOperationException(
                "A finance close request is already pending approval.",
                ex);
        }

        return MapCloseRequest(entity, period.Name);
    }

    public async Task<FinanceCloseRequestDto> ReviewCloseRequestAsync(Guid userId, Guid closeRequestId, ReviewFinanceCloseRequest request, CancellationToken ct = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var entity = await _db.FinanceCloseRequests
                .Include(x => x.AccountingPeriod)
                .FirstOrDefaultAsync(
                    x => x.Id == closeRequestId && !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException(
                    "Finance close request not found.");

            if (entity.Status != FinanceCloseRequestStatus.PendingApproval)
                throw new InvalidOperationException(
                    "Finance close request is no longer pending approval.");

            if (request.Approve && entity.RequestedByUserId == userId)
                throw new InvalidOperationException(
                    "The user who requested period close cannot approve it.");

            entity.ReviewedByUserId = userId;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewNote = request.Note.Trim();
            entity.LastUpdatedAt = DateTime.UtcNow;
            entity.LastUpdatedByUserId = userId;

            if (!request.Approve)
            {
                entity.Status = FinanceCloseRequestStatus.Rejected;
            }
            else
            {
                entity.Status = FinanceCloseRequestStatus.Approved;

                await _accounting.ClosePeriodAsync(
                    userId,
                    entity.AccountingPeriodId,
                    new CloseAccountingPeriodRequest
                    {
                        Note =
                            $"Approved finance close request {entity.Id}: " +
                            request.Note
                    },
                    ct);

                entity.Status = FinanceCloseRequestStatus.Executed;
                entity.ExecutedAt = DateTime.UtcNow;
            }

            _audit.Stage(
                new AuditRecordRequest(
                    request.Approve
                        ? "FINANCE_CLOSE_APPROVED"
                        : "FINANCE_CLOSE_REJECTED",
                    "Finance",
                    nameof(FinanceCloseRequest),
                    entity.Id.ToString(),
                    NewValues: new
                    {
                        entity.Status,
                        entity.ReviewNote,
                        entity.ExecutedAt
                    },
                    UserId: userId));

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return MapCloseRequest(
                entity,
                entity.AccountingPeriod.Name);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(ct);

            throw new InvalidOperationException(
                "Finance close request is no longer pending approval.",
                ex);
        }
    }

    public async Task<FinanceMonthlyClosePackDto> GetMonthlyClosePackAsync(Guid periodId, string? baseCurrencyCode = null, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct); var checklist = await GetCloseChecklistAsync(periodId, ct);
        return new FinanceMonthlyClosePackDto { Period = new AccountingPeriodDto { Id = period.Id, Name = period.Name, StartsAt = period.StartsAt, EndsAt = period.EndsAt, Status = period.Status, ClosedAt = period.ClosedAt, ClosedByUserId = period.ClosedByUserId, CloseNote = period.CloseNote }, IncomeStatement = await GetIncomeStatementAsync(period.StartsAt, period.EndsAt, baseCurrencyCode, ct), BalanceSheet = await GetBalanceSheetAsync(period.EndsAt.AddTicks(-1), baseCurrencyCode, ct), Checklist = checklist.ToList(), ProviderInvoicesPendingReview = await _db.ProviderInvoices.CountAsync(x => !x.IsDeleted && x.InvoiceDate >= period.StartsAt && x.InvoiceDate < period.EndsAt && x.Status != ProviderInvoiceStatus.Posted && x.Status != ProviderInvoiceStatus.Rejected, ct), SettlementBatchesWithVariance = await _db.SettlementBatches.CountAsync(x => !x.IsDeleted && x.WindowEnd > period.StartsAt && x.WindowStart < period.EndsAt && x.Status == SettlementBatchStatus.Variance, ct) };
    }

    private async Task PostProviderInvoiceAsync(ProviderInvoice invoice, Guid userId, CancellationToken ct)
    {
        if (invoice.JournalEntryId.HasValue) { invoice.Status = ProviderInvoiceStatus.Posted; return; }
        await EnsureFinanceAccountsAsync(ct); var accounts = await _db.AccountingAccounts.Where(x => !x.IsDeleted && x.IsActive && new[] { SettlementClearingCode, ProviderFeeExpenseCode, ProviderFeeAdjustmentGainCode, IndirectTaxExpenseCode }.Contains(x.Code)).ToDictionaryAsync(x => x.Code, ct);
        var positiveVariance = invoice.Lines.Where(x => x.VarianceAmount > InvoiceVarianceTolerance).Sum(x => x.VarianceAmount); var negativeVariance = Math.Abs(invoice.Lines.Where(x => x.VarianceAmount < -InvoiceVarianceTolerance).Sum(x => x.VarianceAmount)); var tax = invoice.TaxAmount;
        if (positiveVariance == 0m && negativeVariance == 0m && tax == 0m) { invoice.Status = ProviderInvoiceStatus.Posted; invoice.PostedAt = DateTime.UtcNow; return; }
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(x => !x.IsDeleted && x.StartsAt <= invoice.InvoiceDate && x.EndsAt > invoice.InvoiceDate, ct) ?? throw new InvalidOperationException("Create an open accounting period for the provider invoice date before posting it.");
        if (period.Status != AccountingPeriodStatus.Open) throw new InvalidOperationException($"Accounting period '{period.Name}' is closed.");
        var journal = new JournalEntry { Reference = NewReference("JE"), SourceKey = $"PROVIDER_INVOICE:{invoice.Id}", SourceType = AccountingSourceType.ProviderInvoiceAdjustment, SourceId = invoice.Id, AccountingPeriodId = period.Id, EntryDate = invoice.InvoiceDate, CurrencyCode = invoice.CurrencyCode, Description = $"Provider invoice reconciliation {invoice.ProviderCode} {invoice.InvoiceNumber}", PostedByUserId = userId, CreatedByUserId = userId };
        if (positiveVariance > 0) { journal.Lines.Add(Line(accounts[ProviderFeeExpenseCode], positiveVariance, 0m, "Provider invoice fee variance")); journal.Lines.Add(Line(accounts[SettlementClearingCode], 0m, positiveVariance, "Provider invoice fee variance")); }
        if (negativeVariance > 0) { journal.Lines.Add(Line(accounts[SettlementClearingCode], negativeVariance, 0m, "Provider invoice favorable fee variance")); journal.Lines.Add(Line(accounts[ProviderFeeAdjustmentGainCode], 0m, negativeVariance, "Provider invoice favorable fee variance")); }
        if (tax > 0) { journal.Lines.Add(Line(accounts[IndirectTaxExpenseCode], tax, 0m, "Provider invoice indirect tax")); journal.Lines.Add(Line(accounts[SettlementClearingCode], 0m, tax, "Provider invoice indirect tax")); }
        _db.JournalEntries.Add(journal); invoice.JournalEntryId = journal.Id; invoice.Status = ProviderInvoiceStatus.Posted; invoice.PostedAt = DateTime.UtcNow;
    }

    private async Task EnsureFinanceAccountsAsync(CancellationToken ct)
    {
        var defs = new[] { (SettlementClearingCode, "Provider Settlement Clearing", AccountingAccountType.Asset), (ProviderFeeExpenseCode, "Provider Processing Expense", AccountingAccountType.Expense), (ProviderFeeAdjustmentGainCode, "Provider Fee Adjustment Gain", AccountingAccountType.Revenue), (IndirectTaxExpenseCode, "Indirect Tax Expense", AccountingAccountType.Expense) };
        var existing = await _db.AccountingAccounts.Where(x => defs.Select(d => d.Item1).Contains(x.Code) && !x.IsDeleted).Select(x => x.Code).ToListAsync(ct); foreach (var d in defs.Where(d => !existing.Contains(d.Item1))) _db.AccountingAccounts.Add(new AccountingAccount { Code = d.Item1, Name = d.Item2, Type = d.Item3, IsSystem = true, IsActive = true }); if (_db.ChangeTracker.Entries<AccountingAccount>().Any(x => x.State == EntityState.Added)) await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureChecklistAsync(AccountingPeriod period, CancellationToken ct)
    {
        var defs = new[] { ("ACCOUNTING_SYNC", "Accounting synchronization completed", true, true, 10), ("TRIAL_BALANCE", "Trial balance is balanced", true, true, 20), ("PROVIDER_INVOICES", "Provider invoices reviewed and posted or rejected", true, true, 30), ("SETTLEMENT_VARIANCES", "Settlement variances resolved", true, true, 40), ("TAX_REVIEW", "Tax/VAT/GST review completed", true, false, 50), ("MANAGEMENT_REVIEW", "Finance management review completed", true, false, 60) };
        var existing = await _db.FinanceCloseChecklistItems.Where(x => x.AccountingPeriodId == period.Id && !x.IsDeleted).Select(x => x.Code).ToListAsync(ct); foreach (var d in defs.Where(d => !existing.Contains(d.Item1))) _db.FinanceCloseChecklistItems.Add(new FinanceCloseChecklistItem { AccountingPeriodId = period.Id, Code = d.Item1, Label = d.Item2, IsRequired = d.Item3, IsSystemCheck = d.Item4, SortOrder = d.Item5 }); if (_db.ChangeTracker.Entries<FinanceCloseChecklistItem>().Any(x => x.State == EntityState.Added)) await _db.SaveChangesAsync(ct);
    }

    private async Task RefreshSystemChecklistAsync(AccountingPeriod period, CancellationToken ct)
    {
        var items = await _db.FinanceCloseChecklistItems.Where(x => x.AccountingPeriodId == period.Id && x.IsSystemCheck && !x.IsDeleted).ToListAsync(ct); var now = DateTime.UtcNow;
        var trial = await _accounting.GetTrialBalanceAsync(period.StartsAt, period.EndsAt, ct); var balanced = Math.Abs(trial.Sum(x => x.DebitTotal) - trial.Sum(x => x.CreditTotal)) <= 0.01m;
        var pendingInvoices = await _db.ProviderInvoices.AnyAsync(x => !x.IsDeleted && x.InvoiceDate >= period.StartsAt && x.InvoiceDate < period.EndsAt && x.Status != ProviderInvoiceStatus.Posted && x.Status != ProviderInvoiceStatus.Rejected, ct);
        var settlementVariance = await _db.SettlementBatches.AnyAsync(x => !x.IsDeleted && x.WindowEnd > period.StartsAt && x.WindowStart < period.EndsAt && x.Status == SettlementBatchStatus.Variance, ct);
        foreach (var item in items) { var complete = item.Code switch { "ACCOUNTING_SYNC" => true, "TRIAL_BALANCE" => balanced, "PROVIDER_INVOICES" => !pendingInvoices, "SETTLEMENT_VARIANCES" => !settlementVariance, _ => item.IsCompleted }; if (item.IsCompleted != complete) { item.IsCompleted = complete; item.CompletedAt = complete ? now : null; item.CompletedByUserId = null; item.Note = complete ? "System check passed." : "System check requires attention."; item.LastUpdatedAt = now; } }
    }

    private static bool IsPendingCloseUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        string.Equals(
            postgres.ConstraintName,
            PendingCloseRequestUniqueIndex,
            StringComparison.Ordinal);

    private async Task<AccountingPeriod> GetPeriodAsync(Guid periodId, CancellationToken ct) => await _db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == periodId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Accounting period not found.");
    private async Task<Dictionary<string, decimal>> ResolveRatesAsync(IEnumerable<string> currencies, string baseCode, FinanceTranslationRateType type, DateTime at, CancellationToken ct)
    {
        var distinct = currencies.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [baseCode] = 1m }; var needed = distinct.Where(x => !x.Equals(baseCode, StringComparison.OrdinalIgnoreCase)).ToList(); if (needed.Count == 0) return result;
        var rows = await _db.FinanceTranslationRates.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive && needed.Contains(x.SourceCurrencyCode) && x.BaseCurrencyCode == baseCode && x.RateType == type && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at)).OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct); foreach (var g in rows.GroupBy(x => x.SourceCurrencyCode, StringComparer.OrdinalIgnoreCase)) result[g.Key] = g.First().Rate; return result;
    }

    private static FinancialStatementLineDto StatementLine(string code, string name, AccountingAccountType type, string currency, decimal amount, IReadOnlyDictionary<string, decimal> rates, HashSet<string> missing, bool synthetic = false)
    {
        var normalized = Normalize(currency); decimal? rate = rates.TryGetValue(normalized, out var r) ? r : null; if (!rate.HasValue) missing.Add(normalized); return new FinancialStatementLineDto { AccountCode = code, AccountName = name, AccountType = type, CurrencyCode = normalized, NativeAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero), TranslationRate = rate, BaseAmount = rate.HasValue ? decimal.Round(amount * rate.Value, 2, MidpointRounding.AwayFromZero) : null, IsSynthetic = synthetic };
    }
    private static FinanceTranslationRateDto MapRate(FinanceTranslationRate x) => new() { Id = x.Id, SourceCurrencyCode = x.SourceCurrencyCode, BaseCurrencyCode = x.BaseCurrencyCode, RateType = x.RateType, Rate = x.Rate, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo, Source = x.Source, IsActive = x.IsActive };
    private static ProviderInvoiceDto MapInvoice(ProviderInvoice x, IReadOnlyList<ProviderInvoiceLine> lines) => new() { Id = x.Id, ProviderCode = x.ProviderCode, InvoiceNumber = x.InvoiceNumber, InvoiceDate = x.InvoiceDate, PeriodStart = x.PeriodStart, PeriodEnd = x.PeriodEnd, CurrencyCode = x.CurrencyCode, NetAmount = x.NetAmount, TaxAmount = x.TaxAmount, TotalAmount = x.TotalAmount, MatchedProviderFeeAmount = x.MatchedProviderFeeAmount, VarianceAmount = x.VarianceAmount, Status = x.Status, FileName = x.FileName, Note = x.Note, ReviewNote = x.ReviewNote, ImportedAt = x.ImportedAt, ReviewedAt = x.ReviewedAt, PostedAt = x.PostedAt, JournalEntryId = x.JournalEntryId, Lines = lines.Select(y => new ProviderInvoiceLineDto { Id = y.Id, RowNumber = y.RowNumber, ProviderTransactionId = y.ProviderTransactionId, ProviderReference = y.ProviderReference, Description = y.Description, NetAmount = y.NetAmount, TaxAmount = y.TaxAmount, TotalAmount = y.TotalAmount, IsMatched = y.IsMatched, MatchedProviderFeeAmount = y.MatchedProviderFeeAmount, VarianceAmount = y.VarianceAmount }).ToList() };
    private static TaxRuleDto MapTaxRule(TaxRule x) => new() { Id = x.Id, JurisdictionCode = x.JurisdictionCode, TaxCode = x.TaxCode, Name = x.Name, TaxType = x.TaxType, AppliesTo = x.AppliesTo, RatePercentage = x.RatePercentage, IsInclusive = x.IsInclusive, IsRecoverable = x.IsRecoverable, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo, IsActive = x.IsActive };
    private static FinanceCloseChecklistItemDto MapChecklist(FinanceCloseChecklistItem x) => new() { Id = x.Id, Code = x.Code, Label = x.Label, IsRequired = x.IsRequired, IsSystemCheck = x.IsSystemCheck, IsCompleted = x.IsCompleted, CompletedAt = x.CompletedAt, CompletedByUserId = x.CompletedByUserId, Note = x.Note, SortOrder = x.SortOrder };
    private static FinanceCloseRequestDto MapCloseRequest(FinanceCloseRequest x, string periodName) => new() { Id = x.Id, AccountingPeriodId = x.AccountingPeriodId, PeriodName = periodName, Status = x.Status, RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt, RequestNote = x.RequestNote, ReviewedByUserId = x.ReviewedByUserId, ReviewedAt = x.ReviewedAt, ReviewNote = x.ReviewNote, ExecutedAt = x.ExecutedAt };
    private static JournalLine Line(AccountingAccount account, decimal debit, decimal credit, string narrative) => new() { AccountingAccountId = account.Id, DebitAmount = decimal.Round(debit, 2, MidpointRounding.AwayFromZero), CreditAmount = decimal.Round(credit, 2, MidpointRounding.AwayFromZero), Narrative = narrative };
    private static string Normalize(string value) => value.Trim().ToUpperInvariant(); private static DateTime ToUtc(DateTime x) => x.Kind == DateTimeKind.Utc ? x : x.ToUniversalTime(); private static string NewReference(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    private async Task<AccountingPeriod> GetOrCreatePeriodForDateAsync(DateTime date, CancellationToken ct)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(x => !x.IsDeleted && x.StartsAt <= date && x.EndsAt > date, ct);
        if (period is not null) return period;
        var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc); period = new AccountingPeriod { Name = start.ToString("yyyy-MM"), StartsAt = start, EndsAt = start.AddMonths(1) }; _db.AccountingPeriods.Add(period); await _db.SaveChangesAsync(ct); return period;
    }

    private async Task<JournalEntryDto> LoadJournalAsync(Guid id, CancellationToken ct) => await _db.JournalEntries.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new JournalEntryDto { Id = x.Id, Reference = x.Reference, SourceKey = x.SourceKey, SourceType = x.SourceType, SourceId = x.SourceId, PeriodName = x.AccountingPeriod.Name, EntryDate = x.EntryDate, CurrencyCode = x.CurrencyCode, Description = x.Description, Status = x.Status, Lines = x.Lines.OrderBy(y => y.CreatedAt).Select(y => new JournalLineDto { Id = y.Id, AccountCode = y.AccountingAccount.Code, AccountName = y.AccountingAccount.Name, DebitAmount = y.DebitAmount, CreditAmount = y.CreditAmount, Narrative = y.Narrative }).ToList() }).FirstAsync(ct);

    private sealed record StatementRow(string AccountCode, string AccountName, AccountingAccountType AccountType, string CurrencyCode, decimal Amount);
    private sealed record CurrencyAmountRow(string CurrencyCode, decimal Amount);

    private static Dictionary<string, int> ParseHeaders(string line) => ParseCsvLine(line).Select((x, i) => new { Name = x.Trim().ToLowerInvariant(), Index = i }).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
    private static List<string> ParseCsvLine(string line) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(c); } result.Add(current.ToString()); return result; }
}
