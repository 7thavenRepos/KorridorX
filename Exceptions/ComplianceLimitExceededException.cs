namespace KorridorX.Exceptions;

public sealed class ComplianceLimitExceededException : Exception
{
    public string Code { get; }

    public ComplianceLimitExceededException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
