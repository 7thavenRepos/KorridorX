namespace KorridorX.Models.Lookups;

// Code is the provider's canonical business_type value, never a display label.
public sealed class BusinessType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
