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
(
    Guid Id,
    Guid TransferId,
    string TransferReference,
    TransferStatus TransferStatus,
    string Reference,
    string SourceCountryCode,
    string CurrencyCode,
    decimal Amount,
    PaymentMethod PaymentMethod,
    CollectionStatus Status,
    string ProviderCode,
    string? ProviderCollectionId,
    string? ProviderReference,
    string? CheckoutUrl,
    string? VirtualAccountNumber,
    string? VirtualAccountBankName,
    string? VirtualAccountName,
    DateTime? InitiatedAt,
    DateTime? ConfirmedAt,
    DateTime? FailedAt,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt
);

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
