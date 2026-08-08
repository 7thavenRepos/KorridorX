using KorridorX.Dtos.Finance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Finance;

public interface IFinancialCloseService
{
    Task<IReadOnlyList<FinanceTranslationRateDto>> GetTranslationRatesAsync(string? sourceCurrencyCode = null, string? baseCurrencyCode = null, CancellationToken ct = default);
    Task<FinanceTranslationRateDto> UpsertTranslationRateAsync(Guid userId, UpsertFinanceTranslationRateRequest request, CancellationToken ct = default);
    Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime from, DateTime to, string? baseCurrencyCode = null, CancellationToken ct = default);
    Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOf, string? baseCurrencyCode = null, CancellationToken ct = default);
    Task<PagedResult<ProviderInvoiceDto>> GetProviderInvoicesAsync(ProviderInvoiceStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<ProviderInvoiceDto> GetProviderInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task<ProviderInvoiceDto> ImportProviderInvoiceAsync(Guid userId, ImportProviderInvoiceFormDto request, CancellationToken ct = default);
    Task<ProviderInvoiceDto> ReviewProviderInvoiceAsync(Guid userId, Guid invoiceId, ReviewProviderInvoiceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaxRuleDto>> GetTaxRulesAsync(CancellationToken ct = default);
    Task<TaxRuleDto> UpsertTaxRuleAsync(Guid userId, Guid? taxRuleId, UpsertTaxRuleRequest request, CancellationToken ct = default);
    Task<TaxCalculationDto> CalculateTaxAsync(CalculateTaxRequest request, CancellationToken ct = default);
    Task<AccrualResultDto> CreateAccrualAsync(Guid userId, CreateAccrualRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FinanceCloseChecklistItemDto>> GetCloseChecklistAsync(Guid periodId, CancellationToken ct = default);
    Task<FinanceCloseChecklistItemDto> UpdateChecklistItemAsync(Guid userId, Guid periodId, Guid itemId, UpdateFinanceCloseChecklistItemRequest request, CancellationToken ct = default);
    Task<PagedResult<FinanceCloseRequestDto>> GetCloseRequestsAsync(Guid? periodId, FinanceCloseRequestStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<FinanceCloseRequestDto> SubmitCloseRequestAsync(Guid userId, Guid periodId, CreateFinanceCloseRequest request, CancellationToken ct = default);
    Task<FinanceCloseRequestDto> ReviewCloseRequestAsync(Guid userId, Guid closeRequestId, ReviewFinanceCloseRequest request, CancellationToken ct = default);
    Task<FinanceMonthlyClosePackDto> GetMonthlyClosePackAsync(Guid periodId, string? baseCurrencyCode = null, CancellationToken ct = default);
}
