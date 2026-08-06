using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.BusinessBeneficiaries;

public class BusinessBeneficiary : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public BusinessBeneficiaryType BeneficiaryType { get; set; } = BusinessBeneficiaryType.Individual;
    public string Name { get; set; } = "";
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? Nickname { get; set; }
    public string CountryCode { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? RelationshipOrPurpose { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<BusinessBeneficiaryBankAccount> BankAccounts { get; set; } =
        new List<BusinessBeneficiaryBankAccount>();

    public ICollection<BusinessBeneficiaryMobileWallet> MobileWallets { get; set; } =
        new List<BusinessBeneficiaryMobileWallet>();
}
