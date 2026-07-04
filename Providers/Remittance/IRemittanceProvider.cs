namespace KorridorX.Providers.Remittance;

public interface IRemittanceProvider
{
    string ProviderName { get; }

    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}