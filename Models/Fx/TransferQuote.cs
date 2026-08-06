using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Fx;

public class TransferQuote : AuditableEntity
{
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }

    public string SourceCountryCode { get; set; } = "";
    public string DestinationCountryCode { get; set; } = "";
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public TransferType TransferType { get; set; } = TransferType.ConsumerToConsumer;

    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }

    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }

    public decimal FeeAmount { get; set; }
    public string FeeCurrencyCode { get; set; } = "";

    public decimal TotalPayableAmount { get; set; }

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderQuoteId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
