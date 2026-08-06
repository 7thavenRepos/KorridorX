using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Providers;

public record ProviderBankDto(
    Guid Id,
    ProviderCode ProviderCode,
    string ProviderBankId,
    string Name,
    string Code,
    string? NationalBankCode,
    string CountryCode,
    string? CountryName,
    bool IsActive,
    DateTime LastSyncedAt);

public record ProviderBankSyncResultDto(
    int Received,
    int Inserted,
    int Updated,
    int Deactivated,
    DateTime CompletedAt);

public record RecipientBankVerificationDto(
    Guid RecipientId,
    Guid BankAccountId,
    string ProviderBankId,
    string BankName,
    string AccountNumber,
    string AccountName,
    string ProviderVerifiedAccountName,
    bool IsVerified,
    DateTime VerifiedAt);

public record ProviderTransactionDto(
    Guid Id,
    ProviderCode ProviderCode,
    Guid? TransferId,
    Guid? CollectionId,
    Guid? PayoutId,
    string ProviderTransactionId,
    string? ProviderReference,
    string TransactionType,
    string ProviderStatus,
    string CurrencyCode,
    decimal Amount,
    DateTime? ProviderCreatedAt,
    DateTime LastSyncedAt,
    DateTime CreatedAt);

public record ProviderRequestLogDto(
    Guid Id,
    ProviderCode ProviderCode,
    string Endpoint,
    string HttpMethod,
    ProviderRequestStatus Status,
    int? HttpStatusCode,
    Guid? RelatedTransferId,
    Guid? RelatedCollectionId,
    Guid? RelatedPayoutId,
    string? ErrorMessage,
    long? DurationMs,
    DateTime RequestedAt,
    DateTime? RespondedAt);

public record ProviderRequestLogDetailsDto(
    ProviderRequestLogDto Request,
    string? RequestHeadersJson,
    string? RequestBodyJson,
    string? ResponseBodyJson);

public record ProviderWebhookEventDto(
    Guid Id,
    ProviderCode ProviderCode,
    string? ProviderEventId,
    string EventType,
    WebhookProcessingStatus ProcessingStatus,
    bool IsDuplicate,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    string? ErrorMessage,
    int AttemptCount);

public record ProviderWebhookReplayDto(
    string ProviderTransactionId,
    string Message,
    Guid ProviderRequestLogId);

public record ManualReconciliationDto(
    Guid ProviderTransactionRowId,
    bool Updated,
    string ProviderStatus,
    DateTime ReconciledAt);
