namespace KorridorX.Exceptions;

public sealed class ComplianceHoldException : Exception
{
    public ComplianceHoldException(string message)
        : base(message)
    {
    }
}
