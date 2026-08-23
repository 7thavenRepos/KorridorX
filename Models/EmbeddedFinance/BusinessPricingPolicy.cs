using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class BusinessPricingPolicy : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public string SourceAssetCode { get; set; } = "";
    public string DestinationAssetCode { get; set; } = "";

    // MarkupPercentage is retained as the normalized/legacy percentage view.
    public decimal MarkupPercentage { get; set; }
    public BusinessPricingAdjustmentType AdjustmentType { get; set; } =
        BusinessPricingAdjustmentType.Percentage;
    public decimal AdjustmentValue { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
