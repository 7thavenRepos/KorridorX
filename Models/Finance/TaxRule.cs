using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class TaxRule : AuditableEntity
{
    public string JurisdictionCode { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string Name { get; set; } = "";
    public TaxType TaxType { get; set; }
    public TaxApplicability AppliesTo { get; set; }
    public decimal RatePercentage { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsRecoverable { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
