using KorridorX.Models.Common;

namespace KorridorX.Models.Recipients;

public class RecipientBankAccount : AuditableEntity
{
    public Guid RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;

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

    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public string? ProviderRecipientId { get; set; }
    public string? ProviderBankAccountId { get; set; }
}