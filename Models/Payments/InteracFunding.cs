using KorridorX.Models.Common;

namespace KorridorX.Models.Payments;

public sealed class InteracReceivingRoute : AuditableEntity
{
    public string Environment { get; set; } = "";
    public string ConnectionKey { get; set; } = "";
    public Guid ProviderWalletConfigurationId { get; set; }
    public string ProviderWalletId { get; set; } = "";
    public string ProviderBusinessId { get; set; } = "";
    public string ProviderRegistrationId { get; set; } = "";
    public string ReceivingEmail { get; set; } = "";
    public string NormalizedReceivingEmail { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IndividualEnabled { get; set; }
    public bool BusinessEnabled { get; set; }
    public string Status { get; set; } = "Draft";
    public string? ProviderStatus { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? FailureReason { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class InteracSendingEmail : AuditableEntity
{
    public Guid RouteId { get; set; }
    public Guid FinancialAccountId { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public bool IsDefault { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? VerifiedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CodeHash { get; set; }
    public string? CodeSalt { get; set; }
    public DateTime? CodeExpiresAt { get; set; }
    public DateTime? CanResendAt { get; set; }
    public int FailedAttempts { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

// A verified address is never reassigned to a different wallet on the same route.
// Removing a sender stops future credits but retains this ownership/evidence.
public sealed class InteracSenderOwnership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RouteId { get; set; }
    public string NormalizedEmail { get; set; } = "";
    public Guid FinancialAccountId { get; set; }
    public DateTime EstablishedAt { get; set; } = DateTime.UtcNow;
}

// Shared by requested collections, transfer funding and unsolicited wallet deposits.
// The immutable provider transaction is the claim key; webhook event IDs are not.
public sealed class ProviderCollectionCreditClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderCode { get; set; } = "BLAAIZ";
    public string Environment { get; set; } = "";
    public string ConnectionKey { get; set; } = "";
    public string ProviderTransactionId { get; set; } = "";
    public Guid CollectionId { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
}

public sealed class InteracDeposit : BaseEntity
{
    public string Environment { get; set; } = "";
    public string ConnectionKey { get; set; } = "";
    public string ProviderTransactionId { get; set; } = "";
    public string? ProviderReference { get; set; }
    public string Status { get; set; } = "PendingVerification";
    public string? ReviewReason { get; set; }
    public string? SenderEmail { get; set; }
    public string? ReceivingEmail { get; set; }
    public string CurrencyCode { get; set; } = "CAD";
    public decimal? GrossAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? FeeAmount { get; set; }
    public Guid? RouteId { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public Guid? CollectionId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProviderOccurredAt { get; set; }
    public DateTime? CreditedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string OriginalWebhookJson { get; set; } = "";
    public string? VerifiedProviderJson { get; set; }
}

public sealed class InteracDepositReviewNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DepositId { get; set; }
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
