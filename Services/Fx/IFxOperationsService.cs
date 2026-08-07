using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Fx;

public interface IFxOperationsService
{
    Task<FxMarkupRuleDto> UpsertMarkupRuleAsync(Guid userId, UpsertFxMarkupRuleRequestDto request, CancellationToken ct = default);
    Task<PagedResult<FxMarkupRuleDto>> GetMarkupRulesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ManagedExchangeRateDto> CreateManagedRateAsync(Guid userId, CreateManagedExchangeRateRequestDto request, CancellationToken ct = default);
    Task<ManagedExchangeRateDto> DeactivateRateAsync(Guid userId, Guid rateId, CancellationToken ct = default);
    Task<PagedResult<ManagedExchangeRateDto>> GetRatesAsync(string? sourceCurrencyCode, string? destinationCurrencyCode, bool? active, int page, int pageSize, CancellationToken ct = default);
    Task<FxOperationsDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
