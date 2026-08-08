using KorridorX.Models.Common;

namespace KorridorX.Models.Finance;

public sealed class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public Guid AccountingAccountId { get; set; }
    public AccountingAccount AccountingAccount { get; set; } = null!;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narrative { get; set; }
}
