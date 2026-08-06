using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Fx;

public class TransferQuoteDto
{
    public Guid Id { get; set; }
    public Guid? CustomerProfileId { get; set; }
    public Guid? BusinessProfileId { get; set; }

    public string SourceCountryCode { get; set; } = "";
    public string DestinationCountryCode { get; set; } = "";
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public TransferType TransferType { get; set; }

    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }
    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }
    public decimal FeeAmount { get; set; }
    public string FeeCurrencyCode { get; set; } = "";
    public decimal TotalPayableAmount { get; set; }

    public string ProviderCode { get; set; } = "";
    public string? ProviderQuoteId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
}
