using KorridorX.Models.Common;
using KorridorX.Models.Customers;

namespace KorridorX.Models.Recipients;

public class Recipient : AuditableEntity
{
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    public string? MiddleName { get; set; }
    public string? Nickname { get; set; }

    public string CountryCode { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public string? RelationshipToSender { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RecipientBankAccount> BankAccounts { get; set; } = new List<RecipientBankAccount>();
    public ICollection<RecipientMobileWallet> MobileWallets { get; set; } = new List<RecipientMobileWallet>();
}