using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.BusinessFunding;

public class BusinessLedgerEntry : BaseEntity
{
    public Guid BusinessLedgerTransactionId { get; set; }
    public BusinessLedgerTransaction BusinessLedgerTransaction { get; set; } = null!;

    public Guid? BusinessWalletId { get; set; }
    public BusinessWallet? BusinessWallet { get; set; }

    public BusinessLedgerAccountType AccountType { get; set; }
    public BusinessLedgerEntrySide Side { get; set; }
    public decimal Amount { get; set; }
    public decimal? AccountBalanceAfter { get; set; }
}
