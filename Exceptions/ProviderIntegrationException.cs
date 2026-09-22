namespace KorridorX.Exceptions;

public class ProviderIntegrationException : Exception
{
    public ProviderIntegrationException(
        string message,
        int? providerStatusCode = null,
        string? providerResponse = null,
        Guid? requestLogId = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(message, innerException)
    {
        ProviderStatusCode = providerStatusCode;
        ProviderResponse = providerResponse;
        RequestLogId = requestLogId;
        ValidationErrors = validationErrors;
    }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }
    public int? ProviderStatusCode { get; }
    public string? ProviderResponse { get; }
    public Guid? RequestLogId { get; }
}
