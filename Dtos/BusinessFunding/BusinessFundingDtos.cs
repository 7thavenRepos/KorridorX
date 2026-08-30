using KorridorX.Dtos.Payments;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessFunding;

public record BusinessWalletDto(
    Guid Id,
    Guid BusinessProfileId,
    string CurrencyCode,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record AdminBusinessWalletDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    string? TradingName,
    string? ContactEmail,
    string CountryCode,
    string CurrencyCode,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record AdminBusinessWalletSubjectDto(
    Guid Id,
    string BusinessName,
    string? TradingName,
    string CountryCode,
    string? ContactEmail,
    KybStatus KybStatus);

public record BusinessLedgerEntryDto(
    Guid Id,
    LedgerBalanceBucket AccountType,
    LedgerPostingSide Side,
    decimal Amount,
    decimal? AccountBalanceAfter);

public record BusinessLedgerTransactionDto(
    Guid Id,
    string Reference,
    string CurrencyCode,
    LedgerTransactionType Type,
    LedgerTransactionStatus Status,
    decimal Amount,
    string Description,
    Guid? TransferId,
    Guid? BusinessPaymentBatchId,
    Guid? CollectionId,
    DateTime PostedAt,
    IReadOnlyList<BusinessLedgerEntryDto> Entries,
    Guid? ReversalOfTransactionId = null,
    Guid? ReversedByTransactionId = null,
    DateTime? ReversedAt = null,
    string? ReversalReason = null);

public record AdminCreditBusinessWalletRequestDto(
    Guid BusinessProfileId,
    string CurrencyCode,
    decimal Amount,
    string Reason);

public record BusinessTransferFundingDto(
    Guid TransferId,
    string TransferReference,
    TransferStatus TransferStatus,
    BusinessFundingSource FundingSource,
    string CurrencyCode,
    decimal Amount,
    Guid? BusinessWalletId,
    FinancialReservationStatus? ReservationStatus,
    CollectionDetailsDto? Collection);

public record CreateBusinessExternalCollectionRequestDto(PaymentMethod PaymentMethod);

public record AdminSetBusinessWalletStatusRequestDto(string Reason);

public record AdminAdjustBusinessWalletRequestDto(
    decimal Amount,
    BusinessWalletAdjustmentDirection Direction,
    string Reason);

public record AdminReverseBusinessLedgerTransactionRequestDto(string Reason);
