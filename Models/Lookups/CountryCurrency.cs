namespace KorridorX.Models.Lookups;

public class CountryCurrency
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CountryCode { get; set; } = "";
    public Country Country { get; set; } = null!;

    public string CurrencyCode { get; set; } = "";
    public Currency Currency { get; set; } = null!;

    public bool CanSend { get; set; } = false;
    public bool CanReceive { get; set; } = false;

    public bool IsDefault { get; set; } = false;
}