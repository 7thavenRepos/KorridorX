using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Transfers;

public record CreateTransferRequestDto
(
	Guid TransferQuoteId,
	Guid RecipientId,
	Guid? RecipientBankAccountId,
	Guid? RecipientMobileWalletId,
	TransferPurpose Purpose,
	string? PurposeNote
);

public record TransferDto
(
	Guid Id,
	string Reference,
	Guid CustomerProfileId,
	Guid RecipientId,
	Guid? RecipientBankAccountId,
	Guid? RecipientMobileWalletId,
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
	string FeeCurrencyCode,
	decimal TotalPayableAmount,
	decimal CustomerRate,
	decimal ProviderRate,
	TransferStatus Status,
	string ProviderCode,
	string? ProviderTransferId,
	string? ProviderReference,
	DateTime? PaymentReceivedAt,
	DateTime? PayoutInitiatedAt,
	DateTime? CompletedAt,
	DateTime? FailedAt,
	DateTime? CancelledAt,
	string? FailureReason,
	DateTime CreatedAt,
	DateTime? LastUpdatedAt
);

public record TransferStatusHistoryDto
(
	Guid Id,
	Guid TransferId,
	TransferStatus OldStatus,
	TransferStatus NewStatus,
	string? Reason,
	string? Source,
	Guid? ChangedByUserId,
	DateTime ChangedAt
);

public record TransferTimelineEventDto
(
	Guid Id,
	Guid TransferId,
	string EventType,
	string Title,
	string? Description,
	string? MetadataJson,
	DateTime OccurredAt
);

public record TransferDetailsDto
(
	TransferDto Transfer,
	List<TransferStatusHistoryDto> StatusHistories,
	List<TransferTimelineEventDto> TimelineEvents
);