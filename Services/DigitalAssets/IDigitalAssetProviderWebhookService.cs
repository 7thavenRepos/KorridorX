namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetProviderWebhookService
{
    Task ProcessAsync(
        string providerCode,
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default);
}
