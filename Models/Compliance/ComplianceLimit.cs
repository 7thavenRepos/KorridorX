using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class ComplianceLimit : AuditableEntity
{
    public CustomerType CustomerType { get; set; }

    public string CountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";

    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal PerTransferLimit { get; set; }

    public bool IsActive { get; set; } = true;
}