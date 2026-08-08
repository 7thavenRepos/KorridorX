using KorridorX.Models.Common;
using KorridorX.Models.Providers;

namespace KorridorX.Models.Finance;

public sealed class ProviderInvoiceLine : BaseEntity
{
    public Guid ProviderInvoiceId { get; set; }
    public ProviderInvoice ProviderInvoice { get; set; } = null!;
    public int RowNumber { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReference { get; set; }
    public string Description { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsMatched { get; set; }
    public Guid? ProviderTransactionRowId { get; set; }
    public ProviderTransaction? ProviderTransaction { get; set; }
    public decimal MatchedProviderFeeAmount { get; set; }
    public decimal VarianceAmount { get; set; }
}
