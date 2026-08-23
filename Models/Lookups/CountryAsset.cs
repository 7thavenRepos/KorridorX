namespace KorridorX.Models.Lookups;

public class CountryAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CountryCode { get; set; } = "";
    public Country Country { get; set; } = null!;

    public string AssetCode { get; set; } = "";
    public Asset Asset { get; set; } = null!;

    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
    public bool CanDeposit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanTrade { get; set; }
    public bool CanUseInstant { get; set; }
    public bool IsDefault { get; set; }
}
