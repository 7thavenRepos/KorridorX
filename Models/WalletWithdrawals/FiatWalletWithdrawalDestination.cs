using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Models.WalletWithdrawals;

// A saved destination belongs to one fiat wallet, not a remittance recipient.
// Bank account numbers are protected by the application's persisted Data Protection keys.
public sealed class FiatWalletWithdrawalDestination : AuditableEntity
{
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public FinancialAccountOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public string AssetCode { get; set; } = "";
    public string DestinationCountryCode { get; set; } = "";
    public PaymentMethod PaymentMethod { get; set; }
    public string? ProviderBankId { get; set; }
    public string? BankName { get; set; }
    public string? ProtectedAccountNumber { get; set; }
    public string? AccountNumberLast4 { get; set; }
    public string? VerifiedAccountName { get; set; }
    public string? Email { get; set; }
    public string? VerificationReference { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
