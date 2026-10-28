namespace KorridorX.Exceptions;

public class ProviderIntegrationException : Exception
{
    public ProviderIntegrationException(
        string message,
        int? providerStatusCode = null,
        string? providerResponse = null,
        Guid? requestLogId = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        string? providerErrorCode = null,
        string? providerRequestReference = null)
        : base(message, innerException)
    {
        ProviderStatusCode = providerStatusCode;
        ProviderResponse = providerResponse;
        RequestLogId = requestLogId;
        ValidationErrors = validationErrors;
        ProviderErrorCode = providerErrorCode;
        ProviderRequestReference = providerRequestReference;
    }

    public string? ProviderErrorCode { get; }
    public string? ProviderRequestReference { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }
    public int? ProviderStatusCode { get; }
    public string? ProviderResponse { get; }
    public Guid? RequestLogId { get; }
}
