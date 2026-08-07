using KorridorX.Models.Common;

namespace KorridorX.Models.Treasury;

public class ProviderWalletBalance : BaseEntity
{
    public string ProviderCode { get; set; } = "Blaaiz";
    public string ProviderWalletId { get; set; } = "";
    public string? ProviderBusinessId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
