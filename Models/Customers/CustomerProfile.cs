using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Customers;

public class CustomerProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public CustomerType CustomerType { get; set; } = CustomerType.Individual;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    public string? MiddleName { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public string CountryCode { get; set; } = "";
    public string? StateOrProvince { get; set; }
    public string? City { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }

    public KycStatus KycStatus { get; set; } = KycStatus.NotStarted;
    public DateTime? KycApprovedAt { get; set; }

    public string? BlaaizCustomerId { get; set; }
}