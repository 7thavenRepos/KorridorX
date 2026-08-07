using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Treasury;

public class SettlementBatch : AuditableEntity
{
    public string Reference { get; set; } = "";
    public string ProviderCode { get; set; } = "Blaaiz";
    public string CurrencyCode { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public SettlementBatchStatus Status { get; set; } = SettlementBatchStatus.Draft;
    public decimal GrossInflows { get; set; }
    public decimal GrossOutflows { get; set; }
    public decimal ExpectedNetAmount { get; set; }
    public decimal? ActualNetAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? ReconciliationNote { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public ICollection<SettlementBatchItem> Items { get; set; } = new List<SettlementBatchItem>();
}
