using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Payments;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetWithdrawal : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid BusinessCustomerId { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;
    public Guid DestinationId { get; set; }
    public DigitalAssetWithdrawalDestination Destination { get; set; } = null!;
    public Guid PayoutId { get; set; }
    public Payout Payout { get; set; } = null!;
    public Guid ReservationId { get; set; }
    public FinancialReservation Reservation { get; set; } = null!;
    public string ProviderCode { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public decimal Amount { get; set; }
    // NetworkFee is the quoted/configured fee reserved at withdrawal creation.
    public decimal NetworkFee { get; set; }
    public decimal? ActualNetworkFee { get; set; }
    public decimal? NetworkFeeVariance { get; set; }
    public decimal TotalDebitAmount { get; set; }
    public DigitalAssetWithdrawalStatus Status { get; set; } = DigitalAssetWithdrawalStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
}
