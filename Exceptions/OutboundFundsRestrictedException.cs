namespace KorridorX.Exceptions;

public sealed class OutboundFundsRestrictedException : Exception
{
    public const string ErrorCode = "OUTBOUND_FUNDS_RESTRICTED";

    public const string PublicMessage =
        "Outgoing transactions are currently unavailable on this account. Please contact support.";

    public OutboundFundsRestrictedException()
        : base(PublicMessage)
    {
    }

    public string Code => ErrorCode;
}
