using KorridorX.Models.Common;

namespace KorridorX.Models.Treasury;

public class LiquidityThreshold : AuditableEntity
{
    public string ProviderCode { get; set; } = "Blaaiz";
    public string CurrencyCode { get; set; } = "";
    public decimal MinimumBalance { get; set; }
    public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; } = true;
}
