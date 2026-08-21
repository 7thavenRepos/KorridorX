namespace KorridorX.Services.EmbeddedFinance;
public interface IEmbeddedFinanceCredentialAuthenticator
{
    Task<EmbeddedFinancePrincipal?> AuthenticateAsync(string apiKey, string? remoteIpAddress, CancellationToken ct = default);
}
