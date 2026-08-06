using KorridorX.Models.BusinessTransfers;
using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.BusinessFunding;

public class BusinessLedgerTransaction : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public string Reference { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public BusinessLedgerTransactionType Type { get; set; }
    public BusinessLedgerTransactionStatus Status { get; set; } = BusinessLedgerTransactionStatus.Posted;
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";

    public string? IdempotencyKey { get; set; }
    public string? IdempotencyRequestHash { get; set; }

    public Guid? ReversalOfTransactionId { get; set; }
    public Guid? ReversedByTransactionId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public Guid? BusinessPaymentBatchId { get; set; }
    public BusinessPaymentBatch? BusinessPaymentBatch { get; set; }

    public Guid? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BusinessLedgerEntry> Entries { get; set; } = new List<BusinessLedgerEntry>();
}
