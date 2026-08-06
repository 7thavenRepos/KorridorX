using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessTransfers;

public record ImportBusinessPaymentBatchRequestDto(
    string Name,
    string SourceCountryCode,
    string SourceCurrencyCode,
    BusinessFundingSource FundingSource = BusinessFundingSource.BusinessWallet);

public record BusinessPaymentBatchDecisionRequestDto(string? Comment);

public record BusinessPaymentBatchItemDto(
    Guid Id,
    int RowNumber,
    string? ExternalReference,
    Guid? BusinessBeneficiaryId,
    string? BeneficiaryName,
    Guid? BusinessBeneficiaryBankAccountId,
    Guid? BusinessBeneficiaryMobileWalletId,
    string DestinationCountryCode,
    string DestinationCurrencyCode,
    decimal SourceAmount,
    decimal DestinationAmount,
    decimal FeeAmount,
    decimal TotalPayableAmount,
    TransferPurpose Purpose,
    string? PurposeNote,
    BusinessPaymentBatchItemStatus Status,
    string? ValidationErrors,
    Guid? TransferQuoteId,
    Guid? TransferId);

public record BusinessPaymentBatchDto(
    Guid Id,
    string Reference,
    string Name,
    string? OriginalFileName,
    string SourceCountryCode,
    string SourceCurrencyCode,
    BusinessFundingSource FundingSource,
    BusinessPaymentBatchStatus Status,
    int TotalItems,
    int ValidItems,
    int InvalidItems,
    int CompletedItems,
    int FailedItems,
    decimal TotalSourceAmount,
    decimal TotalFeeAmount,
    decimal TotalPayableAmount,
    int RequiredApprovals,
    int ApprovalCount,
    DateTime? ValidatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    DateTime? CompletedAt,
    string? RejectionReason,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record BusinessPaymentBatchDetailsDto(
    BusinessPaymentBatchDto Batch,
    IReadOnlyList<BusinessPaymentBatchItemDto> Items,
    IReadOnlyList<BusinessApprovalDto> Approvals);
