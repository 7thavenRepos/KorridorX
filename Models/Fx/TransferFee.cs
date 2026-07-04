using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Fx;

public class TransferFee : AuditableEntity
{
    public string SourceCountryCode { get; set; } = "";
    public string DestinationCountryCode { get; set; } = "";

    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";

    public TransferType TransferType { get; set; }

    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public decimal FixedFee { get; set; }
    public decimal PercentageFee { get; set; }

    public string FeeCurrencyCode { get; set; } = "";

    public bool IsActive { get; set; } = true;
}