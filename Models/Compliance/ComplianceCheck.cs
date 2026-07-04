using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Compliance;

public class ComplianceCheck : BaseEntity
{
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public string CheckType { get; set; } = "";
    public string Status { get; set; } = "";

    public string? ProviderCode { get; set; }
    public string? ProviderReference { get; set; }

    public string? ResultJson { get; set; }

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}