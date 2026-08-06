using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Payments;

public record PayoutDto
(
    Guid Id,
    Guid TransferId,
    string TransferReference,
    TransferStatus TransferStatus,
    string Reference,
    string CurrencyCode,
    decimal Amount,
    PaymentMethod PaymentMethod,
    PayoutStatus Status,
    string ProviderCode,
    string? ProviderPayoutId,
    string? ProviderReference,
    string? InteracQuestion,
    string? InteracAnswer,
    DateTime? InitiatedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt,
    DateTime? ReversedAt,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt
);

public record PayoutAttemptDto
(
    Guid Id,
    Guid PayoutId,
    ProviderRequestStatus Status,
    string? ProviderRequestId,
    string? ProviderResponseId,
    string? RequestPayloadJson,
    string? ResponsePayloadJson,
    DateTime AttemptedAt,
    string? ErrorMessage
);

public record PayoutDetailsDto
(
    PayoutDto Payout,
    List<PayoutAttemptDto> Attempts
);
