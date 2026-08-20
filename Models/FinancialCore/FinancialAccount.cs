using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.FinancialCore;

public class FinancialAccount : AuditableEntity
{
    public FinancialAccountOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    public string AccountCode { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public Asset Asset { get; set; } = null!;
    public FinancialAccountType AccountType { get; set; } = FinancialAccountType.Customer;
    public FinancialAccountStatus Status { get; set; } = FinancialAccountStatus.Active;

    public decimal SettledBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal HeldBalance { get; set; }

    public ICollection<LedgerPosting> LedgerPostings { get; set; } = new List<LedgerPosting>();
    public ICollection<FinancialReservation> Reservations { get; set; } = new List<FinancialReservation>();
}
