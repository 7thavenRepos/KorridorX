namespace KorridorX.Exceptions;

public sealed class UnauthorizedApiException : UnauthorizedAccessException
{
    public UnauthorizedApiException(
        string message,
        string code = "UNAUTHORIZED")
        : base(message)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? "UNAUTHORIZED"
            : code.Trim().ToUpperInvariant();
    }

    public string Code { get; }
}
