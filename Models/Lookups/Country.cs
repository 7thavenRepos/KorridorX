namespace KorridorX.Models.Lookups;

public class Country
{
    public string Code { get; set; } = ""; // NG, US, CA
    public string Name { get; set; } = "";
    public string? Iso3Code { get; set; }
    public bool IsSupported { get; set; } = true;
    public bool IsSendCountry { get; set; } = false;
    public bool IsReceiveCountry { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}