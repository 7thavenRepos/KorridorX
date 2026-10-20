using KorridorX.Dtos.Fx;
using KorridorX.Models.Fx;

namespace KorridorX.Services.Fx;

public enum FxRateUse { Transfer, Instant, Preview, Valuation }

public interface ILiveFxRateService
{
    // The callback, observation and rate are committed with the same pair lock and transaction.
    Task<T> UseFreshRateAsync<T>(string source, string destination, FxRateUse use,
        Func<ExchangeRate, Guid, CancellationToken, Task<T>> consume, CancellationToken ct = default);
}

public interface IFxProviderRateService
{
    Task<FxProviderOptionsDto> GetOptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FxProviderConfigurationDto>> GetConfigurationsAsync(CancellationToken ct = default);
    Task<FxProviderPreviewDto> PreviewAsync(SaveFxProviderConfigurationRequestDto request, CancellationToken ct = default);
    Task<FxProviderConfigurationDto> SaveAsync(Guid userId, Guid? id, SaveFxProviderConfigurationRequestDto request, CancellationToken ct = default);
    Task<FxProviderConfigurationDto> ActivateAsync(Guid userId, Guid id, long revision, CancellationToken ct = default);
    Task<FxProviderConfigurationDto> DeactivateAsync(Guid userId, Guid id, long revision, CancellationToken ct = default);
    Task<FxProviderConfigurationDto> RefreshAsync(Guid userId, Guid id, long revision, CancellationToken ct = default);
    Task RefreshDueAsync(CancellationToken ct = default);
}
