using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Payments;

public record InitiateRefundRequestDto(string? Reason);

public record RefundDto(
    Guid CollectionId,
    Guid TransferId,
    string CollectionReference,
    string CurrencyCode,
    decimal Amount,
    CollectionStatus CollectionStatus,
    string? ProviderRefundId,
    string? ProviderRefundReference,
    string? RefundReason,
    string? RefundFailureReason,
    DateTime? RefundInitiatedAt,
    DateTime? RefundedAt,
    DateTime? LastRefundSyncedAt);
