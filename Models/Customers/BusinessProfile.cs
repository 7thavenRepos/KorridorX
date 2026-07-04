using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Customers;

public class BusinessProfile : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public ApplicationUser OwnerUser { get; set; } = null!;

    public string BusinessName { get; set; } = "";
    public string? RegistrationNumber { get; set; }
    public string? TaxIdentificationNumber { get; set; }

    public string CountryCode { get; set; } = "";
    public string? StateOrProvince { get; set; }
    public string? City { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    public KybStatus KybStatus { get; set; } = KybStatus.NotStarted;
    public DateTime? KybApprovedAt { get; set; }

    public string? BlaaizBusinessCustomerId { get; set; }

    public ICollection<BusinessUser> Users { get; set; } = new List<BusinessUser>();
}