using KorridorX.Models.Common;

namespace KorridorX.Models.Finance;

public sealed class FinanceCloseChecklistItem : AuditableEntity
{
    public Guid AccountingPeriodId { get; set; }
    public AccountingPeriod AccountingPeriod { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsSystemCheck { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}
