using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class FinanceTranslationRate : AuditableEntity
{
    public string SourceCurrencyCode { get; set; } = "";
    public string BaseCurrencyCode { get; set; } = "";
    public FinanceTranslationRateType RateType { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Source { get; set; } = "Manual";
    public bool IsActive { get; set; } = true;
}
