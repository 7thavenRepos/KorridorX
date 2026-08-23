using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.EmbeddedFinance;

public class BusinessCustomer : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;
    public string ExternalReference { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string CountryCode { get; set; } = "";
    public Country Country { get; set; } = null!;
    public BusinessCustomerStatus Status { get; set; } = BusinessCustomerStatus.Active;
    public string? MetadataJson { get; set; }
    public ICollection<CollectionAccount> CollectionAccounts { get; set; } = new List<CollectionAccount>();
}
