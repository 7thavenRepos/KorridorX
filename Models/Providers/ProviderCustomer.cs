using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Providers;

public class ProviderCustomer : AuditableEntity
{
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }

    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;

    public string ProviderCustomerId { get; set; } = "";

    public string? ProviderStatus { get; set; }
    public string? MetadataJson { get; set; }

    public DateTime? LastSyncedAt { get; set; }
}