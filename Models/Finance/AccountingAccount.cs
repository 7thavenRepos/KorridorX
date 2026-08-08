using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Finance;

public sealed class AccountingAccount : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountingAccountType Type { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
