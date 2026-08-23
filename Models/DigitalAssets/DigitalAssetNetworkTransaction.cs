using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Payments;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetNetworkTransaction : AuditableEntity
{
    public string ProviderCode { get; set; } = "";
    public string ProviderTransactionId { get; set; } = "";
    public string? ProviderReference { get; set; }
    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;
    public string AssetCode { get; set; } = "";
    public DigitalAssetTransactionDirection Direction { get; set; }
    public DigitalAssetTransactionStatus Status { get; set; } = DigitalAssetTransactionStatus.Observed;
    public string? TransactionHash { get; set; }
    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }
    public string? DestinationTag { get; set; }
    public decimal Amount { get; set; }
    public decimal NetworkFee { get; set; }
    public int Confirmations { get; set; }
    public int RequiredConfirmations { get; set; }
    public long? BlockNumber { get; set; }
    public Guid? CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public Guid? PayoutId { get; set; }
    public Payout? Payout { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public LedgerTransaction? LedgerTransaction { get; set; }
    public string? RawPayloadJson { get; set; }
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
}
