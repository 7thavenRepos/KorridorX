using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public sealed class OutboundFundsRestriction : AuditableEntity
{
    public OutboundFundsRestrictionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectDisplayName { get; set; } = "";

    public OutboundFundsRestrictionSource Source { get; set; }
    public string InternalReason { get; set; } = "";
    public string? ExternalReference { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid AppliedByUserId { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public Guid? LiftedByUserId { get; set; }
    public DateTime? LiftedAt { get; set; }
    public string? LiftReason { get; set; }
}
