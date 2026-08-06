namespace KorridorX.Providers.Remittance.Blaaiz;

public interface IBlaaizTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
