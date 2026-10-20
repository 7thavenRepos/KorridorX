using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Fx;
using KorridorX.Models.Treasury;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Fx;

public sealed class FxOperationsService : IFxOperationsService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly TreasuryOptions _options;

    public FxOperationsService(AppDbContext db, IAuditService audit, IOptions<TreasuryOptions> options)
    {
        _db = db;
        _audit = audit;
        _options = options.Value;
    }

    public Task<FxMarkupRuleDto> UpsertMarkupRuleAsync(Guid userId, UpsertFxMarkupRuleRequestDto request, CancellationToken ct = default) =>
        throw new InvalidOperationException("Use FX provider configurations to select an existing pair, fetch a live provider rate, set its markup and activate it. Legacy manual rate and markup entries remain available as history.");

    public async Task<PagedResult<FxMarkupRuleDto>> GetMarkupRulesAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var paged = await _db.FxMarkupRules.AsNoTracking().Where(x => !x.IsDeleted)
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<FxMarkupRuleDto> { Meta = paged.Meta, Items = paged.Items.Select(ToRuleDto).ToList() };
    }

    public Task<ManagedExchangeRateDto> CreateManagedRateAsync(Guid userId, CreateManagedExchangeRateRequestDto request, CancellationToken ct = default) =>
        throw new InvalidOperationException("Use FX provider configurations to select an existing pair, fetch a live provider rate, set its markup and activate it. Legacy manual rate and markup entries remain available as history.");

    public async Task<ManagedExchangeRateDto> DeactivateRateAsync(Guid userId, Guid rateId, CancellationToken ct = default)
    {
        var rate = await _db.ExchangeRates.FirstOrDefaultAsync(x => x.Id == rateId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Exchange rate not found.");
        if (rate.FxProviderRateConfigurationId.HasValue)
            throw new InvalidOperationException("Deactivate the provider configuration to stop live pricing for this pair.");
        if (rate.IsActive)
        {
            rate.IsActive = false;
            rate.EffectiveTo ??= DateTime.UtcNow;
            rate.LastUpdatedAt = DateTime.UtcNow;
            rate.LastUpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);
            await _audit.RecordAsync(new AuditRecordRequest("ExchangeRateDeactivated", "FX", nameof(ExchangeRate), rate.Id.ToString(), UserId: userId), ct);
        }
        return ToRateDto(rate);
    }

    public async Task<PagedResult<ManagedExchangeRateDto>> GetRatesAsync(string? sourceCurrencyCode, string? destinationCurrencyCode, bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.ExchangeRates.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(sourceCurrencyCode)) { var source = Normalize(sourceCurrencyCode); query = query.Where(x => x.SourceCurrencyCode == source); }
        if (!string.IsNullOrWhiteSpace(destinationCurrencyCode)) { var destination = Normalize(destinationCurrencyCode); query = query.Where(x => x.DestinationCurrencyCode == destination); }
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
        var paged = await query.OrderByDescending(x => x.EffectiveFrom).PaginateAsync(page, pageSize, ct);
        return new PagedResult<ManagedExchangeRateDto> { Meta = paged.Meta, Items = paged.Items.Select(ToRateDto).ToList() };
    }

    public async Task<FxOperationsDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdueBefore = now.AddMinutes(-2);
        var rates = await _db.ExchangeRates.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode).ToListAsync(ct);
        return new FxOperationsDashboardDto
        {
            GeneratedAt = now,
            ActiveRateCount = rates.Count,
            StaleRateCount = await _db.FxProviderRateConfigurations.CountAsync(x => !x.IsDeleted && x.IsActive &&
                (x.LastError != null || (x.ScheduleMode != "Disabled" && x.NextRefreshAtUtc < overdueBefore)), ct),
            ActiveMarkupRuleCount = await _db.FxProviderRateConfigurations.CountAsync(x => !x.IsDeleted && x.IsActive, ct),
            Rates = rates.Select(ToRateDto).ToList()
        };
    }

    private static decimal CalculateCustomerRate(decimal providerRate, FxMarkupRule? rule)
    {
        var customerRate = rule is null ? providerRate : providerRate * (1m - (rule.MarkupPercentage / 100m));
        if (rule?.MinimumCustomerRate is decimal min && customerRate < min) customerRate = min;
        if (rule?.MaximumCustomerRate is decimal max && customerRate > max) customerRate = max;
        if (customerRate <= 0) throw new InvalidOperationException("The configured FX markup produces an invalid customer rate.");
        return Math.Round(customerRate, 8, MidpointRounding.AwayFromZero);
    }

    private static FxMarkupRuleDto ToRuleDto(FxMarkupRule x) => new()
    {
        Id = x.Id, SourceCurrencyCode = x.SourceCurrencyCode, DestinationCurrencyCode = x.DestinationCurrencyCode,
        MarkupPercentage = x.MarkupPercentage, MinimumCustomerRate = x.MinimumCustomerRate,
        MaximumCustomerRate = x.MaximumCustomerRate, IsActive = x.IsActive
    };

    private static ManagedExchangeRateDto ToRateDto(ExchangeRate x) => new()
    {
        Id = x.Id, SourceCurrencyCode = x.SourceCurrencyCode, DestinationCurrencyCode = x.DestinationCurrencyCode,
        ProviderRate = x.ProviderRate, CustomerRate = x.CustomerRate, MarkupRate = x.MarkupRate,
        ProviderCode = x.ProviderCode, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo,
        IsActive = x.IsActive, CreatedAt = x.CreatedAt
    };

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Currency code is required.");
        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 20) throw new InvalidOperationException("Asset code cannot exceed 20 characters.");
        return code;
    }
}
