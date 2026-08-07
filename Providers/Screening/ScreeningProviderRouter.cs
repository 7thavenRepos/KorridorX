using KorridorX.Configuration;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Screening;

public sealed class ScreeningProviderRouter : ISanctionsScreeningProvider
{
    private readonly ComplianceScreeningOptions _options;
    private readonly ConfiguredWatchlistScreeningProvider _configured;
    private readonly OpenSanctionsScreeningProvider _openSanctions;

    public ScreeningProviderRouter(
        IOptions<ComplianceScreeningOptions> options,
        ConfiguredWatchlistScreeningProvider configured,
        OpenSanctionsScreeningProvider openSanctions)
    {
        _options = options.Value;
        _configured = configured;
        _openSanctions = openSanctions;
    }

    public string ProviderCode => ResolveProvider().ProviderCode;

    public Task<ScreeningProviderResult> ScreenAsync(
        ScreeningProviderRequest request,
        CancellationToken ct = default) =>
        ResolveProvider().ScreenAsync(request, ct);

    private ISanctionsScreeningProvider ResolveProvider()
    {
        if (string.Equals(_options.ProviderCode, "OpenSanctions", StringComparison.OrdinalIgnoreCase))
            return _openSanctions;
        if (string.Equals(_options.ProviderCode, "ConfiguredWatchlist", StringComparison.OrdinalIgnoreCase))
            return _configured;

        throw new InvalidOperationException(
            $"Unsupported compliance screening provider '{_options.ProviderCode}'.");
    }
}
