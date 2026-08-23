using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetDepositAddress : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid BusinessCustomerId { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;
    public string ProviderCode { get; set; } = "";
    public string? ProviderAddressId { get; set; }
    public string Address { get; set; } = "";
    public string? DestinationTag { get; set; }
    public DigitalAssetAddressStatus Status { get; set; } = DigitalAssetAddressStatus.Active;
    public DateTime? LastUsedAt { get; set; }
}
