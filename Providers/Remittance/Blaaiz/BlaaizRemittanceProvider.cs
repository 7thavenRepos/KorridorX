namespace KorridorX.Providers.Remittance.Blaaiz;

public class BlaaizRemittanceProvider : IRemittanceProvider
{
    public string ProviderName => "Blaaiz";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}