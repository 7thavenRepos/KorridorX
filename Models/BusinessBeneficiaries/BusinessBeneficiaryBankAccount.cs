using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.BusinessBeneficiaries;

public class BusinessBeneficiaryBankAccount : AuditableEntity
{
    public Guid BusinessBeneficiaryId { get; set; }
    public BusinessBeneficiary BusinessBeneficiary { get; set; } = null!;

    public string CountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string BankName { get; set; } = "";
    public string? BankCode { get; set; }
    public string? BranchCode { get; set; }
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string? Iban { get; set; }
    public string? SwiftBic { get; set; }
    public string? RoutingNumber { get; set; }
    public string? SortCode { get; set; }
    public PaymentMethod? PayoutMethod { get; set; }
    public string? RecipientType { get; set; }
    public string? AccountType { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    public string? ProviderBankId { get; set; }
    public string? ProviderBeneficiaryId { get; set; }
    public string? ProviderBankAccountId { get; set; }
    public string? ProviderVerifiedAccountName { get; set; }
    public string? ProviderVerificationReference { get; set; }
    public DateTime? VerificationAttemptedAt { get; set; }
    public string? LastVerificationError { get; set; }

    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
