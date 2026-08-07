using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Treasury;

public sealed class TreasuryRebalanceRequest : AuditableEntity
{
    public string Reference { get; set; } = "";
    public string ProviderCode { get; set; } = "Blaaiz";

    public Guid FromProviderWalletBalanceId { get; set; }
    public ProviderWalletBalance FromProviderWalletBalance { get; set; } = null!;

    public Guid ToProviderWalletBalanceId { get; set; }
    public ProviderWalletBalance ToProviderWalletBalance { get; set; } = null!;

    public string FromCurrencyCode { get; set; } = "";
    public string ToCurrencyCode { get; set; } = "";
    public decimal RequestedAmount { get; set; }
    public TreasurySwapAmountType AmountType { get; set; } = TreasurySwapAmountType.From;
    public TreasuryRebalanceStatus Status { get; set; } = TreasuryRebalanceStatus.PendingApproval;
    public string? Reason { get; set; }

    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public string? ProviderSwapId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReference { get; set; }
    public decimal? FromAmount { get; set; }
    public decimal? FromAmountMinusFees { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? CustomExchangeRate { get; set; }
    public string? FailureReason { get; set; }
}
