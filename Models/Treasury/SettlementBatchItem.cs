using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;

namespace KorridorX.Models.Treasury;

public class SettlementBatchItem : BaseEntity
{
    public Guid SettlementBatchId { get; set; }
    public SettlementBatch SettlementBatch { get; set; } = null!;
    public Guid ProviderTransactionRowId { get; set; }
    public ProviderTransaction ProviderTransaction { get; set; } = null!;
    public SettlementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
}
