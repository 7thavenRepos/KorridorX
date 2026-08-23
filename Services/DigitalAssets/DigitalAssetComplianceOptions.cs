namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetComplianceOptions
{
    public bool RequireAddressScreening { get; set; }
    public decimal TravelRuleThreshold { get; set; } = 1000m;
    public int AddressRiskCacheMinutes { get; set; } = 60;
    public decimal BlockingRiskScore { get; set; } = 0.80m;
}
