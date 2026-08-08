using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace KorridorX.Dtos.Finance;

public sealed class UpsertFinanceTranslationRateRequest
{
    [Required, MaxLength(10)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(10)] public string BaseCurrencyCode { get; set; } = "";
    public FinanceTranslationRateType RateType { get; set; }
    [Range(typeof(decimal), "0.0000000001", "999999999999")] public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [MaxLength(100)] public string Source { get; set; } = "Manual";
}

public sealed class FinanceTranslationRateDto
{
    public Guid Id { get; set; }
    public string SourceCurrencyCode { get; set; } = "";
    public string BaseCurrencyCode { get; set; } = "";
    public FinanceTranslationRateType RateType { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Source { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class FinancialStatementLineDto
{
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public AccountingAccountType AccountType { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal NativeAmount { get; set; }
    public decimal? TranslationRate { get; set; }
    public decimal? BaseAmount { get; set; }
    public bool IsSynthetic { get; set; }
}

public sealed class IncomeStatementDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string BaseCurrencyCode { get; set; } = "";
    public List<FinancialStatementLineDto> Revenue { get; set; } = new();
    public List<FinancialStatementLineDto> Expenses { get; set; } = new();
    public decimal? BaseRevenueTotal { get; set; }
    public decimal? BaseExpenseTotal { get; set; }
    public decimal? BaseNetIncome { get; set; }
    public List<string> MissingTranslationCurrencies { get; set; } = new();
}

public sealed class BalanceSheetDto
{
    public DateTime AsOf { get; set; }
    public string BaseCurrencyCode { get; set; } = "";
    public List<FinancialStatementLineDto> Assets { get; set; } = new();
    public List<FinancialStatementLineDto> Liabilities { get; set; } = new();
    public List<FinancialStatementLineDto> Equity { get; set; } = new();
    public decimal? BaseAssetsTotal { get; set; }
    public decimal? BaseLiabilitiesAndEquityTotal { get; set; }
    public decimal? BaseDifference { get; set; }
    public List<string> MissingTranslationCurrencies { get; set; } = new();
}

public sealed class ImportProviderInvoiceFormDto
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;
    [Required, MaxLength(150)] public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "";
    [Required] public IFormFile File { get; set; } = null!;
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class ProviderInvoiceLineDto
{
    public Guid Id { get; set; }
    public int RowNumber { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReference { get; set; }
    public string Description { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsMatched { get; set; }
    public decimal MatchedProviderFeeAmount { get; set; }
    public decimal VarianceAmount { get; set; }
}

public sealed class ProviderInvoiceDto
{
    public Guid Id { get; set; }
    public ProviderCode ProviderCode { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal MatchedProviderFeeAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public ProviderInvoiceStatus Status { get; set; }
    public string FileName { get; set; } = "";
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime ImportedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public List<ProviderInvoiceLineDto> Lines { get; set; } = new();
}

public sealed class ReviewProviderInvoiceRequest
{
    public bool Approve { get; set; }
    [Required, MaxLength(2000)] public string Note { get; set; } = "";
}

public class UpsertTaxRuleRequest
{
    [Required, MaxLength(10)] public string JurisdictionCode { get; set; } = "";
    [Required, MaxLength(50)] public string TaxCode { get; set; } = "";
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    public TaxType TaxType { get; set; }
    public TaxApplicability AppliesTo { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal RatePercentage { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsRecoverable { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TaxRuleDto : UpsertTaxRuleRequest
{
    public Guid Id { get; set; }
}

public sealed class CalculateTaxRequest
{
    [Required, MaxLength(10)] public string JurisdictionCode { get; set; } = "";
    public TaxApplicability AppliesTo { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal Amount { get; set; }
    public DateTime? At { get; set; }
}

public sealed class TaxCalculationLineDto
{
    public string TaxCode { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal RatePercentage { get; set; }
    public bool IsInclusive { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class TaxCalculationDto
{
    public decimal InputAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AmountExcludingTax { get; set; }
    public decimal AmountIncludingTax { get; set; }
    public List<TaxCalculationLineDto> Taxes { get; set; } = new();
}

public sealed class CreateAccrualRequest
{
    public DateTime EntryDate { get; set; }
    public DateTime? ReversalDate { get; set; }
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "";
    [Required, MaxLength(1000)] public string Description { get; set; } = "";
    public Guid ExpenseAccountId { get; set; }
    public Guid LiabilityAccountId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal Amount { get; set; }
}

public sealed class AccrualResultDto
{
    public JournalEntryDto AccrualJournal { get; set; } = new();
    public JournalEntryDto? ReversalJournal { get; set; }
}

public sealed class FinanceCloseChecklistItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsSystemCheck { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateFinanceCloseChecklistItemRequest
{
    public bool IsCompleted { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class CreateFinanceCloseRequest
{
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class ReviewFinanceCloseRequest
{
    public bool Approve { get; set; }
    [Required, MaxLength(2000)] public string Note { get; set; } = "";
}

public sealed class FinanceCloseRequestDto
{
    public Guid Id { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public string PeriodName { get; set; } = "";
    public FinanceCloseRequestStatus Status { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ExecutedAt { get; set; }
}

public sealed class FinanceMonthlyClosePackDto
{
    public AccountingPeriodDto Period { get; set; } = new();
    public IncomeStatementDto IncomeStatement { get; set; } = new();
    public BalanceSheetDto BalanceSheet { get; set; } = new();
    public List<FinanceCloseChecklistItemDto> Checklist { get; set; } = new();
    public int ProviderInvoicesPendingReview { get; set; }
    public int SettlementBatchesWithVariance { get; set; }
}
