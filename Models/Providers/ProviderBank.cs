using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Providers;

public class ProviderBank : BaseEntity
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;

    public string ProviderBankId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? NationalBankCode { get; set; }

    public string? ProviderCountryId { get; set; }
    public string CountryCode { get; set; } = "";
    public string? CountryName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    public string? RawPayloadJson { get; set; }
}
