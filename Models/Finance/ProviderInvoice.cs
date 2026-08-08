using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class ProviderInvoice : AuditableEntity
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal MatchedProviderFeeAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public ProviderInvoiceStatus Status { get; set; } = ProviderInvoiceStatus.Imported;
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string? Note { get; set; }
    public Guid ImportedByUserId { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public DateTime? PostedAt { get; set; }
    public ICollection<ProviderInvoiceLine> Lines { get; set; } = new List<ProviderInvoiceLine>();
}
