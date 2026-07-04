using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Providers;

public class PaymentProvider : AuditableEntity
{
    public ProviderCode Code { get; set; } = ProviderCode.Blaaiz;

    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public string? MetadataJson { get; set; }
}