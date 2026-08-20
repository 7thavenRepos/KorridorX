using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.FinancialCore;

public class FinancialReservation : AuditableEntity
{
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;

    public FinancialReservationType Type { get; set; }
    public string RelatedEntityType { get; set; } = "";
    public Guid RelatedEntityId { get; set; }
    public string? ContextEntityType { get; set; }
    public Guid? ContextEntityId { get; set; }

    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public FinancialReservationStatus Status { get; set; } = FinancialReservationStatus.Active;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CapturedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
