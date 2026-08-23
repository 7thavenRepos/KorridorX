using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetWithdrawalDestination : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid BusinessCustomerId { get; set; }
    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;
    public string AssetCode { get; set; } = "";
    public string Address { get; set; } = "";
    public string? DestinationTag { get; set; }
    public string? Label { get; set; }
    public DigitalAssetDestinationStatus Status { get; set; } = DigitalAssetDestinationStatus.Active;
}
