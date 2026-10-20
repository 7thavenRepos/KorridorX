namespace KorridorX.Providers.Fx;

public interface IFxRateProvider
{
    string ProviderCode { get; }
    string ConnectionKey { get; }
    bool IsAvailable { get; }

    // Every invocation reads the provider. Historical snapshots cannot satisfy this call.
    Task<FxProviderRate> GetRateAsync(
        string sourceCurrencyCode,
        string destinationCurrencyCode,
        string? providerRateId = null,
        CancellationToken ct = default);
}

public sealed record FxProviderRate(
    string SourceCurrencyCode,
    string DestinationCurrencyCode,
    decimal Rate,
    string ProviderRateId,
    DateTime? ProviderTimestampUtc,
    DateTime RequestedAtUtc,
    DateTime ReceivedAtUtc,
    string ConnectionKey,
    Guid RequestLogId,
    bool SendEnabled,
    bool SwapEnabled)
{
    public string? ProviderCategory { get; init; }
    public string? ProviderRateType { get; init; }
}

public interface IBlaaizFxRateTokenService
{
    string ConnectionKey { get; }
    Task<string> GetRatesReadAccessTokenAsync(CancellationToken ct = default);
}
