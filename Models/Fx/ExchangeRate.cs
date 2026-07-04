using KorridorX.Models.Common;

namespace KorridorX.Models.Fx;

public class ExchangeRate : AuditableEntity
{
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";

    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }

    public decimal MarkupRate { get; set; }

    public string ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderRateId { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}