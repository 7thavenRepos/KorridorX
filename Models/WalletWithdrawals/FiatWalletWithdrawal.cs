using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;

namespace KorridorX.Models.WalletWithdrawals;

// Immutable initiation snapshot. Future W2B settlement always uses this
// record + its reservation, never mutable destination or current wallet fees.
public sealed class FiatWalletWithdrawal : AuditableEntity
{
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public FinancialAccountOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? CustomerProfileId { get; set; }
    public Guid? BusinessProfileId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string SourceCountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public int CurrencyDecimalPlaces { get; set; }

    public Guid DestinationId { get; set; }
    public FiatWalletWithdrawalDestination Destination { get; set; } = null!;
    public Guid PaymentChannelId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? BankName { get; set; }
    public string? ProviderBankId { get; set; }
    public string? ProtectedAccountNumber { get; set; }
    public string? AccountNumberLast4 { get; set; }
    public string? VerifiedAccountName { get; set; }
    public string? RecipientEmail { get; set; }

    public string Reference { get; set; } = "";
    // The caller's key is never stored in plaintext. Hash is scoped by owner.
    public string IdempotencyKeyHash { get; set; } = "";
    public string RequestFingerprint { get; set; } = "";
    public decimal WithdrawalAmount { get; set; }
    public decimal ProviderFee { get; set; }
    public decimal KorridorXFee { get; set; }
    public string KorridorXFeePolicy { get; set; } = "NOT_CONFIGURED";
    public decimal TotalWalletDebit { get; set; }
    public decimal ExpectedRecipientAmount { get; set; }
    public DateTime FeeQuotedAtUtc { get; set; }

    public FiatWalletWithdrawalStatus Status { get; set; } = FiatWalletWithdrawalStatus.AwaitingExecution;
    public Guid? FinancialReservationId { get; set; }
    public FinancialReservation? FinancialReservation { get; set; }
    public Guid? PayoutId { get; set; }
    public Payout? Payout { get; set; }
    public DateTime? SubmittedToProviderAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public string? OperationalHoldReason { get; set; }
}
