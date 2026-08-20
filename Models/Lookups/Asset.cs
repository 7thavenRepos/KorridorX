using KorridorX.Models.Enums;

namespace KorridorX.Models.Lookups;

public class Asset
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public AssetType Type { get; set; } = AssetType.Fiat;
    public int DecimalPlaces { get; set; } = 2;
    public bool IsStablecoin { get; set; }
    public bool IsSupported { get; set; } = true;
    public bool DepositEnabled { get; set; } = true;
    public bool WithdrawalEnabled { get; set; } = true;
    public bool TradingEnabled { get; set; } = true;
    public bool InstantEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }

    public ICollection<AssetNetwork> Networks { get; set; } = new List<AssetNetwork>();
}
