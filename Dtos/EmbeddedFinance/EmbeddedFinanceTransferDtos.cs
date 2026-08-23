using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record CreateEmbeddedTransferQuoteRequestDto(
    string DestinationCountryCode,
    string DestinationCurrencyCode,
    decimal SourceAmount,
    TransferType TransferType);

public sealed record CreateEmbeddedTransferRequestDto(
    Guid TransferQuoteId,
    Guid BusinessBeneficiaryId,
    Guid BusinessBeneficiaryBankAccountId,
    TransferPurpose Purpose,
    string? PurposeNote,
    string ExternalReference);

public sealed record EmbeddedTransferDto(
    Guid Id,
    string Reference,
    string? ExternalReference,
    Guid BusinessCustomerId,
    Guid SourceFinancialAccountId,
    Guid TransferQuoteId,
    Guid BusinessBeneficiaryId,
    Guid? BusinessBeneficiaryBankAccountId,
    TransferType TransferType,
    TransferPurpose Purpose,
    string? PurposeNote,
    string SourceCountryCode,
    string DestinationCountryCode,
    string SourceCurrencyCode,
    string DestinationCurrencyCode,
    decimal SourceAmount,
    decimal DestinationAmount,
    decimal FeeAmount,
    decimal TotalPayableAmount,
    decimal CustomerRate,
    TransferStatus Status,
    string ProviderCode,
    string? ProviderTransferId,
    string? ProviderReference,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? PaymentReceivedAt,
    DateTime? PayoutInitiatedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt);

public sealed record EmbeddedTransferCreateResultDto(
    EmbeddedTransferDto Transfer,
    bool PayoutDispatched);
