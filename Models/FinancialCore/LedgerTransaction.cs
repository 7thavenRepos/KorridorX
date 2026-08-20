using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.FinancialCore;

public class LedgerTransaction : AuditableEntity
{
    public string Reference { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public Asset Asset { get; set; } = null!;
    public LedgerTransactionType Type { get; set; }
    public LedgerTransactionStatus Status { get; set; } = LedgerTransactionStatus.Posted;
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";

    public string? IdempotencyScope { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? IdempotencyRequestHash { get; set; }

    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? ContextEntityType { get; set; }
    public Guid? ContextEntityId { get; set; }

    public Guid? ReversalOfTransactionId { get; set; }
    public LedgerTransaction? ReversalOfTransaction { get; set; }

    public Guid? ReversedByTransactionId { get; set; }
    public LedgerTransaction? ReversedByTransaction { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LedgerPosting> Postings { get; set; } = new List<LedgerPosting>();
}
