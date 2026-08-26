using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Payments;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetDepositIntent : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid? BusinessCustomerId { get; set; }

    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;

    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;

    public string ProviderCode { get; set; } = "";
    public string? ProviderWalletId { get; set; }
    public string? ProviderCollectionId { get; set; }
    public string? ProviderReference { get; set; }

    public string AssetCode { get; set; } = "";
    public string NetworkCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Address { get; set; }

    public CollectionStatus Status { get; set; } = CollectionStatus.Pending;
    public DateTime? ProviderExpiresAt { get; set; }
    public DateTime? InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }

    public Guid? CollectionId { get; set; }
    public Collection? Collection { get; set; }
}