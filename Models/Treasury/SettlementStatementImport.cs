using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Treasury;

public sealed class SettlementStatementImport : AuditableEntity
{
    public Guid SettlementBatchId { get; set; }
    public SettlementBatch SettlementBatch { get; set; } = null!;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string CurrencyCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public SettlementStatementImportStatus Status { get; set; } = SettlementStatementImportStatus.Imported;
    public int RowCount { get; set; }
    public int MatchedRowCount { get; set; }
    public int UnmatchedRowCount { get; set; }
    public decimal GrossCredits { get; set; }
    public decimal GrossDebits { get; set; }
    public decimal NetAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? Note { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SettlementStatementItem> Items { get; set; } = new List<SettlementStatementItem>();
}
