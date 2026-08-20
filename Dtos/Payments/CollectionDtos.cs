using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Payments;

public record CreateCollectionRequestDto
(
    PaymentMethod PaymentMethod
);

public record CollectionPaymentMethodDto
(
    PaymentMethod PaymentMethod,
    string Code,
    string DisplayName
);

public record CollectionDto
{
    public Guid Id { get; init; }
    public Guid? TransferId { get; init; }
    public PaymentOperationPurpose Purpose { get; init; }
    public Guid? FinancialAccountId { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string? ContextEntityType { get; init; }
    public Guid? ContextEntityId { get; init; }
    public string? TransferReference { get; init; }
    public TransferStatus? TransferStatus { get; init; }
    public string Reference { get; init; } = "";
    public string? SourceCountryCode { get; init; }
    public string CurrencyCode { get; init; } = "";
    public decimal Amount { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public CollectionStatus Status { get; init; }
    public string ProviderCode { get; init; } = "";
    public string? ProviderCollectionId { get; init; }
    public string? ProviderReference { get; init; }
    public string? CheckoutUrl { get; init; }
    public DateTime? ProviderExpiresAt { get; init; }
    public string? VirtualAccountNumber { get; init; }
    public string? VirtualAccountBankName { get; init; }
    public string? VirtualAccountName { get; init; }
    public DateTime? InitiatedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? FailedAt { get; init; }
    public DateTime? ExpiredAt { get; init; }
    public DateTime? RefundInitiatedAt { get; init; }
    public DateTime? RefundedAt { get; init; }
    public string? ProviderRefundId { get; init; }
    public string? ProviderRefundReference { get; init; }
    public string? RefundReason { get; init; }
    public string? RefundFailureReason { get; init; }
    public DateTime? LastRefundSyncedAt { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
}

public record CollectionAttemptDto
(
    Guid Id,
    Guid CollectionId,
    ProviderRequestStatus Status,
    string? ProviderRequestId,
    string? ProviderResponseId,
    string? RequestPayloadJson,
    string? ResponsePayloadJson,
    DateTime AttemptedAt,
    string? ErrorMessage
);

public record CollectionDetailsDto
(
    CollectionDto Collection,
    List<CollectionAttemptDto> Attempts
);

public record InitiateCollectionRequestDto
(
    string? RedirectUrl,
    CardCollectionDetailsDto? Card,
    string? PayerEmail,
    string? CustomerName,
    int? InteracExpiryHours
);

public record CardCollectionDetailsDto
(
    string CardHolderName,
    string CardNumber,
    string Expiry,
    string Cvc
);
