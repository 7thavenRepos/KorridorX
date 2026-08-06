using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessTransfers;

public record CreateBusinessTransferQuoteRequestDto(
    string SourceCountryCode,
    string DestinationCountryCode,
    string SourceCurrencyCode,
    string DestinationCurrencyCode,
    decimal SourceAmount,
    TransferType TransferType);

public record CreateBusinessTransferRequestDto(
    Guid TransferQuoteId,
    Guid BusinessBeneficiaryId,
    Guid? BusinessBeneficiaryBankAccountId,
    Guid? BusinessBeneficiaryMobileWalletId,
    TransferPurpose Purpose,
    string? PurposeNote,
    BusinessFundingSource FundingSource = BusinessFundingSource.BusinessWallet);

public record BusinessTransferDecisionRequestDto(string? Comment);

public record BusinessTransferDto(
    Guid Id,
    string Reference,
    Guid BusinessProfileId,
    Guid BusinessBeneficiaryId,
    string BeneficiaryName,
    Guid? BusinessBeneficiaryBankAccountId,
    Guid? BusinessBeneficiaryMobileWalletId,
    Guid TransferQuoteId,
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
    BusinessFundingSource FundingSource,
    BusinessApprovalStatus ApprovalStatus,
    int RequiredApprovals,
    int ApprovalCount,
    Guid? RequestedByUserId,
    DateTime? SubmittedForApprovalAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? ApprovalRejectionReason,
    string? ProviderTransferId,
    string? ProviderReference,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record BusinessApprovalDto(
    Guid Id,
    Guid ActionedByUserId,
    BusinessApprovalAction Action,
    string? Comment,
    DateTime ActionedAt);

public record BusinessTransferTimelineDto(
    string EventType,
    string Title,
    string? Description,
    DateTime OccurredAt);

public record BusinessTransferDetailsDto(
    BusinessTransferDto Transfer,
    IReadOnlyList<BusinessApprovalDto> Approvals,
    IReadOnlyList<BusinessTransferTimelineDto> Timeline);

public record BusinessTransferReportDto(
    int TotalTransfers,
    int PendingApproval,
    int Processing,
    int Completed,
    int Failed,
    decimal TotalSourceAmount,
    decimal TotalDestinationAmount,
    IReadOnlyDictionary<string, decimal> SourceTotalsByCurrency,
    IReadOnlyDictionary<string, decimal> DestinationTotalsByCurrency);

public record AddBusinessUserRequestDto(
    string Email,
    BusinessUserRole Role,
    BusinessPermission Permissions);

public record UpdateBusinessUserAccessRequestDto(
    BusinessUserRole Role,
    BusinessPermission Permissions,
    bool IsActive);

public record BusinessUserDto(
    Guid Id,
    Guid UserId,
    string Email,
    BusinessUserRole Role,
    BusinessPermission Permissions,
    bool IsActive,
    bool IsOwner,
    DateTime CreatedAt);

public record UpdateBusinessApprovalPolicyRequestDto(
    bool RequiresTransferApproval,
    int RequiredTransferApprovals,
    decimal? TransferApprovalThreshold,
    bool AllowTransferCreatorApproval);

public record BusinessApprovalPolicyDto(
    bool RequiresTransferApproval,
    int RequiredTransferApprovals,
    decimal? TransferApprovalThreshold,
    bool AllowTransferCreatorApproval);
