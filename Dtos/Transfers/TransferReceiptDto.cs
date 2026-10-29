namespace KorridorX.Dtos.Transfers;

// Decimal strings preserve the booked amounts when a mobile client creates a PDF.
// Receipt generation never obtains a new quote or changes a financial record.
public sealed record TransferReceiptDto(
    Guid TransferId,
    string Reference,
    string Status,
    string SenderName,
    string RecipientName,
    string SourceCurrencyCode,
    string SourceAmount,
    string DestinationCurrencyCode,
    string DestinationAmount,
    string FeeCurrencyCode,
    string FeeAmount,
    string TotalPayableAmount,
    string CustomerRate,
    string? BankName,
    string? MaskedDestination,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime IssuedAt);
