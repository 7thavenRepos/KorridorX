using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class JournalEntry : AuditableEntity
{
    public string Reference { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public AccountingSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public AccountingPeriod AccountingPeriod { get; set; } = null!;
    public DateTime EntryDate { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string Description { get; set; } = "";
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;
    public Guid? ReversalOfJournalEntryId { get; set; }
    public JournalEntry? ReversalOfJournalEntry { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public Guid? PostedByUserId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedByUserId { get; set; }
    public string? ReversalReason { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}
