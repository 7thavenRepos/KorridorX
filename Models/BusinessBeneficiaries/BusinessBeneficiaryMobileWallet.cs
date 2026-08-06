using KorridorX.Models.Common;

namespace KorridorX.Models.BusinessBeneficiaries;

public class BusinessBeneficiaryMobileWallet : AuditableEntity
{
    public Guid BusinessBeneficiaryId { get; set; }
    public BusinessBeneficiary BusinessBeneficiary { get; set; } = null!;

    public string CountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string WalletNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string? ProviderBeneficiaryId { get; set; }
    public string? ProviderWalletId { get; set; }

    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
