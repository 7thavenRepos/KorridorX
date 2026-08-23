namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedWebhookUrlSecurityValidator
{
    Task<string> ValidateAsync(string value, CancellationToken ct = default);
}
