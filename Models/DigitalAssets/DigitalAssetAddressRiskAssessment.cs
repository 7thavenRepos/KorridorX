using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetAddressRiskAssessment : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid BusinessCustomerId { get; set; }

    public Guid AssetNetworkId { get; set; }
    public AssetNetwork AssetNetwork { get; set; } = null!;

    public string AssetCode { get; set; } = "";
    public string Address { get; set; } = "";
    public DigitalAssetAddressScreeningDirection Direction { get; set; }

    public string ProviderCode { get; set; } = "";
    public string? ProviderReference { get; set; }
    public decimal RiskScore { get; set; }
    public DigitalAssetAddressRiskLevel RiskLevel { get; set; }
    public bool IsBlocking { get; set; }
    public string? ReasonsJson { get; set; }
    public string? RawResultJson { get; set; }

    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
