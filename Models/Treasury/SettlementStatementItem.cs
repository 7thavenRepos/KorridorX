using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Treasury;

public sealed class SettlementStatementItem : BaseEntity
{
    public Guid SettlementStatementImportId { get; set; }
    public SettlementStatementImport SettlementStatementImport { get; set; } = null!;

    public int RowNumber { get; set; }
    public string ProviderTransactionId { get; set; } = "";
    public string? ProviderReference { get; set; }
    public string TransactionType { get; set; } = "";
    public SettlementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string? ProviderStatus { get; set; }
    public DateTime? OccurredAt { get; set; }
    public bool IsMatched { get; set; }
    public Guid? ProviderTransactionRowId { get; set; }
}
