using KorridorX.Models.Common;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.Treasury;

public class ProviderWalletBalance : BaseEntity
{
    public string ProviderCode { get; set; } = "Blaaiz";
    public string ProviderWalletId { get; set; } = "";
    public string? ProviderBusinessId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public Guid? AssetNetworkId { get; set; }
    public AssetNetwork? AssetNetwork { get; set; }
    public string? NetworkCode { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
