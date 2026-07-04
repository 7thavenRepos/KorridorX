using KorridorX.Models.Common;
using KorridorX.Models.Customers;

namespace KorridorX.Models.Fx;

public class TransferQuote : AuditableEntity
{
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";

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

    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}