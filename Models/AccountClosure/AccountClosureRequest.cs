using KorridorX.Models.Common;

namespace KorridorX.Models.AccountClosure;

public enum AccountClosureScope
{
    Personal = 1,
    Business = 2
}

public enum AccountClosureRequestStatus
{
    Requested = 1,
    Withdrawn = 2,
    // Reserved for a later, separately reviewed and guarded settlement/retention flow.
    Completed = 3
}

// A request is NOT closure. User access, identity data, linked financial
// accounts and provider resources remain unchanged until separately approved.
public sealed class AccountClosureRequest : AuditableEntity
{
    public AccountClosureScope Scope { get; set; }
    public Guid SubjectId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public AccountClosureRequestStatus Status { get; set; } = AccountClosureRequestStatus.Requested;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
