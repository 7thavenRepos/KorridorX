using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Providers;

public enum PayoutDestinationType
{
    RecipientBankAccount = 1,
    RecipientMobileWallet = 2,
    BusinessBeneficiaryBankAccount = 3,
    BusinessBeneficiaryMobileWallet = 4
}

public class PayoutDestinationProviderMapping : AuditableEntity
{
    public PayoutDestinationType DestinationType { get; set; }
    public Guid DestinationId { get; set; }

    public ProviderCode ProviderCode { get; set; }

    // Provider-specific directory / routing identifier for the destination bank.
    public string? ProviderBankId { get; set; }

    // Provider-side recipient / beneficiary / counterparty identifier.
    public string? ProviderPartyId { get; set; }

    // Provider-side bank-account or wallet identifier.
    public string? ProviderDestinationId { get; set; }

    public bool IsVerified { get; set; }
    public DateTime? VerificationAttemptedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public string? ProviderVerifiedAccountName { get; set; }
    public string? ProviderVerificationReference { get; set; }
    public string? LastVerificationError { get; set; }

    public bool IsActive { get; set; } = true;

    // Reserved for provider-specific data that does not belong in the core destination.
    public string? MetadataJson { get; set; }
}
