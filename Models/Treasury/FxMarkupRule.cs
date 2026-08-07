using KorridorX.Models.Common;

namespace KorridorX.Models.Treasury;

public class FxMarkupRule : AuditableEntity
{
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public bool IsActive { get; set; } = true;
}
