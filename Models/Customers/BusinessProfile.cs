using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Customers;

public class BusinessProfile : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public ApplicationUser OwnerUser { get; set; } = null!;

    public string BusinessName { get; set; } = "";
    public string? TradingName { get; set; }
    public string? BusinessType { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxIdentificationNumber { get; set; }
    public DateTime? IncorporationDate { get; set; }
    public string? IndustryType { get; set; }
    public string? BusinessDescription { get; set; }
    public string? Website { get; set; }
    public string? SourceOfFunds { get; set; }
    public string? EstimatedAnnualRevenue { get; set; }
    public int? ExpectedMonthlyPayments { get; set; }
    public string? AccountPurpose { get; set; }
    public BusinessKybScope KybScope { get; set; } = BusinessKybScope.Full;

    public string CountryCode { get; set; } = "";
    public string? StateOrProvince { get; set; }
    public string? City { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }

    public string? OperatingCountryCode { get; set; }
    public string? OperatingStateOrProvince { get; set; }
    public string? OperatingCity { get; set; }
    public string? OperatingAddressLine1 { get; set; }
    public string? OperatingAddressLine2 { get; set; }
    public string? OperatingPostalCode { get; set; }

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    public KybStatus KybStatus { get; set; } = KybStatus.NotStarted;
    public DateTime? KybSubmittedAt { get; set; }
    public DateTime? KybApprovedAt { get; set; }
    public DateTime? KybRejectedAt { get; set; }
    public string? KybRejectionReason { get; set; }

    public string? BlaaizBusinessCustomerId { get; set; }

    public ICollection<BusinessUser> Users { get; set; } = new List<BusinessUser>();
}