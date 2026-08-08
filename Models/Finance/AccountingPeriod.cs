using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class AccountingPeriod : AuditableEntity
{
    public string Name { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? CloseNote { get; set; }
}
