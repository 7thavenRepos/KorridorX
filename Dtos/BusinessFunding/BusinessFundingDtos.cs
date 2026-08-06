using KorridorX.Dtos.Payments;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessFunding;

public record BusinessWalletDto(
    Guid Id,
    Guid BusinessProfileId,
    string CurrencyCode,
    BusinessWalletStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record BusinessLedgerEntryDto(
    Guid Id,
    BusinessLedgerAccountType AccountType,
    BusinessLedgerEntrySide Side,
    decimal Amount,
    decimal? AccountBalanceAfter);

public record BusinessLedgerTransactionDto(
    Guid Id,
    string Reference,
    string CurrencyCode,
    BusinessLedgerTransactionType Type,
    BusinessLedgerTransactionStatus Status,
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
    BusinessWalletReservationStatus? ReservationStatus,
    CollectionDetailsDto? Collection);

public record CreateBusinessExternalCollectionRequestDto(PaymentMethod PaymentMethod);

public record AdminSetBusinessWalletStatusRequestDto(string Reason);

public record AdminAdjustBusinessWalletRequestDto(
    decimal Amount,
    BusinessWalletAdjustmentDirection Direction,
    string Reason);

public record AdminReverseBusinessLedgerTransactionRequestDto(string Reason);
