using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Compliance;

public class AmlFlag : AuditableEntity
{
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public string FlagType { get; set; } = "";
    public string Severity { get; set; } = "";

    public string Description { get; set; } = "";

    public int RiskScore { get; set; }
    public bool IsBlocking { get; set; }
    public string? ReviewDecision { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}