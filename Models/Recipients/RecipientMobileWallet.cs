using KorridorX.Models.Common;

namespace KorridorX.Models.Recipients;

public class RecipientMobileWallet : AuditableEntity
{
    public Guid RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;

    public string CountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";

    public string ProviderName { get; set; } = "";
    public string WalletNumber { get; set; } = "";
    public string AccountName { get; set; } = "";

    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public string? ProviderRecipientId { get; set; }
    public string? ProviderWalletId { get; set; }
}