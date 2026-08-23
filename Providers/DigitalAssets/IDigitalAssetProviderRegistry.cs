namespace KorridorX.Providers.DigitalAssets;

public interface IDigitalAssetProviderRegistry
{
    IDigitalAssetProvider GetRequired(string providerCode);
    IDigitalAssetProvider GetRequired(string providerCode, string assetCode, string networkCode);
}

public sealed class DigitalAssetProviderRegistry : IDigitalAssetProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IDigitalAssetProvider> _providers;

    public DigitalAssetProviderRegistry(IEnumerable<IDigitalAssetProvider> providers)
    {
        _providers = providers.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    public IDigitalAssetProvider GetRequired(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            throw new InvalidOperationException("Digital-asset provider code is required.");

        if (!_providers.TryGetValue(providerCode.Trim(), out var provider))
            throw new InvalidOperationException(
                $"Digital-asset provider '{providerCode}' is not configured in this deployment.");

        return provider;
    }

    public IDigitalAssetProvider GetRequired(string providerCode, string assetCode, string networkCode)
    {
        var provider = GetRequired(providerCode);

        if (!provider.Supports(assetCode, networkCode))
            throw new InvalidOperationException($"Digital-asset provider '{providerCode}' does not support {assetCode} on {networkCode}.");

        return provider;
    }
}
