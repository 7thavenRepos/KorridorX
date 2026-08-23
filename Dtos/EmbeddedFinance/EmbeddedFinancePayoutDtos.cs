using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record CreateEmbeddedPayoutRequestDto(
    string ExternalReference,
    Guid BusinessBeneficiaryBankAccountId,
    decimal Amount,
    string? Note);

public sealed record EmbeddedPayoutDto(
    Guid Id,
    Guid BusinessCustomerId,
    Guid CollectionAccountId,
    Guid FinancialAccountId,
    string ExternalReference,
    Guid BusinessBeneficiaryBankAccountId,
    string CurrencyCode,
    decimal Amount,
    PaymentMethod PaymentMethod,
    PayoutStatus Status,
    string ProviderCode,
    string? ProviderPayoutId,
    string? ProviderReference,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? InitiatedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt,
    DateTime? ReversedAt);
