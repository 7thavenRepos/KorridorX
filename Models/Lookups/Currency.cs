namespace KorridorX.Models.Lookups;

public class Currency
{
    public string Code { get; set; } = ""; // USD, CAD, NGN
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public int DecimalPlaces { get; set; } = 2;
    public bool IsFiat { get; set; } = true;
    public bool IsStablecoin { get; set; } = false;
    public bool IsSupported { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}