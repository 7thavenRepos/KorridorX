using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.BusinessFunding;

public class BusinessWallet : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public string CurrencyCode { get; set; } = "";
    public BusinessWalletStatus Status { get; set; } = BusinessWalletStatus.Active;

    public decimal SettledBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal HeldBalance { get; set; }

    public ICollection<BusinessLedgerEntry> LedgerEntries { get; set; } = new List<BusinessLedgerEntry>();
    public ICollection<BusinessWalletReservation> Reservations { get; set; } = new List<BusinessWalletReservation>();
}
