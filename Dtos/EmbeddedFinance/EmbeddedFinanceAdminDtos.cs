using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record EmbeddedFinanceAdminOverviewDto(
    int Businesses,
    int ApiApplications,
    int ActiveApiApplications,
    int ActiveCredentials,
    int BusinessCustomers,
    int ActiveBusinessCustomers,
    int CollectionAccounts,
    int ActiveCollectionAccounts,
    int WebhookEndpoints,
    int ActiveWebhookEndpoints,
    int PendingWebhookDeliveries,
    int RetryWebhookDeliveries,
    int DeadLetterWebhookDeliveries);

public sealed record EmbeddedFinanceAdminBusinessDto(
    Guid BusinessProfileId,
    string BusinessName,
    string CountryCode,
    KybStatus KybStatus,
    int ApiApplications,
    int ActiveApiApplications,
    int ActiveCredentials,
    int BusinessCustomers,
    int CollectionAccounts,
    int WebhookEndpoints,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminApplicationDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    string Name,
    string? Description,
    EmbeddedFinanceScope Scopes,
    ApiApplicationStatus Status,
    IReadOnlyList<string> AllowedIpRanges,
    int CredentialCount,
    int ActiveCredentialCount,
    DateTime? LastAuthenticatedAt,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminCredentialDto(
    Guid Id,
    Guid ApiApplicationId,
    string Name,
    string KeyId,
    string SecretLastFour,
    ApiCredentialStatus Status,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    string? LastUsedIpAddress,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminCustomerDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    string ExternalReference,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string CountryCode,
    BusinessCustomerStatus Status,
    int CollectionAccountCount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminCollectionAccountDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    Guid BusinessCustomerId,
    string BusinessCustomerName,
    string ExternalReference,
    string AssetCode,
    Guid FinancialAccountId,
    CollectionAccountStatus Status,
    FinancialAccountStatus FinancialAccountStatus,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance,
    int ProviderMappingCount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminWebhookEndpointDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    Guid ApiApplicationId,
    string ApiApplicationName,
    string Url,
    IReadOnlyList<string> EventTypes,
    BusinessWebhookEndpointStatus Status,
    string SigningSecretLastFour,
    int MaxAttempts,
    int PendingDeliveries,
    int RetryDeliveries,
    int DeadLetterDeliveries,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminWebhookDeliveryDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    Guid EndpointId,
    string EndpointUrl,
    string EventId,
    string EventType,
    BusinessWebhookDeliveryStatus Status,
    int AttemptCount,
    DateTime? NextAttemptAt,
    DateTime? LastAttemptAt,
    DateTime? DeliveredAt,
    DateTime? DeadLetteredAt,
    int? LastResponseStatusCode,
    string? ErrorMessage,
    DateTime CreatedAt);

public sealed record EmbeddedFinanceAdminProviderMappingDto(
    Guid Id,
    Guid CollectionAccountId,
    string ProviderCode,
    string? ProviderCustomerId,
    string? ProviderAccountId,
    string? ProviderReference,
    string? AccountNumber,
    string? AccountName,
    string? BankName,
    ProviderAccountMappingStatus Status,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record EmbeddedFinanceAdminProvisioningRetryResultDto(
    EmbeddedFinanceAdminProviderMappingDto Mapping,
    CollectionAccountStatus CollectionAccountStatus,
    DateTime UpdatedAt);

public sealed record EmbeddedFinanceAdminActivityDto(
    Guid Id,
    string ActivityType,
    string Direction,
    Guid BusinessProfileId,
    string BusinessName,
    Guid BusinessCustomerId,
    string BusinessCustomerName,
    Guid? CollectionAccountId,
    string? CollectionAccountReference,
    Guid? FinancialAccountId,
    string? Reference,
    string? ExternalReference,
    string AssetCode,
    string? DestinationAssetCode,
    decimal Amount,
    decimal? SecondaryAmount,
    decimal? FeeAmount,
    string Status,
    string? ProviderCode,
    string? ProviderReference,
    Guid? ProviderRequestLogId,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime OccurredAt,
    DateTime? FinalizedAt,
    string? FailureReason);


public sealed record EmbeddedFinanceAdminApiContextDto(
    Guid ApiApplicationId,
    string ApiApplicationName,
    string ResourceType,
    Guid ResourceId,
    DateTime CreatedAt);

public sealed record EmbeddedFinanceAdminProviderRequestTraceDto(
    Guid Id,
    string ProviderCode,
    string Status,
    int? ResponseStatusCode,
    bool HasError,
    DateTime RequestedAt,
    DateTime? RespondedAt,
    long? DurationMs);

public sealed record EmbeddedFinanceAdminProviderTransactionTraceDto(
    Guid Id,
    string ProviderTransactionId,
    string? ProviderReference,
    string TransactionType,
    string ProviderStatus,
    string CurrencyCode,
    decimal Amount,
    DateTime LastSyncedAt,
    DateTime CreatedAt);

public sealed record EmbeddedFinanceAdminReservationTraceDto(
    Guid Id,
    string Type,
    string RelatedEntityType,
    Guid RelatedEntityId,
    string? ContextEntityType,
    Guid? ContextEntityId,
    string Reference,
    decimal Amount,
    decimal CapturedAmount,
    decimal ReleasedAmount,
    decimal RemainingAmount,
    string Status,
    DateTime ReservedAt,
    DateTime? CapturedAt,
    DateTime? ReleasedAt,
    string? ReleaseReason);

public sealed record EmbeddedFinanceAdminLedgerPostingTraceDto(
    Guid Id,
    Guid FinancialAccountId,
    string BalanceBucket,
    string Side,
    decimal Amount,
    decimal? AccountBalanceAfter);

public sealed record EmbeddedFinanceAdminLedgerTransactionTraceDto(
    Guid Id,
    string Reference,
    string AssetCode,
    string Type,
    string Status,
    decimal Amount,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string? ContextEntityType,
    Guid? ContextEntityId,
    DateTime PostedAt,
    DateTime? ReversedAt,
    string? ReversalReason,
    IReadOnlyList<EmbeddedFinanceAdminLedgerPostingTraceDto> Postings);

public sealed record EmbeddedFinanceAdminJournalLineTraceDto(
    Guid Id,
    Guid AccountingAccountId,
    decimal DebitAmount,
    decimal CreditAmount);

public sealed record EmbeddedFinanceAdminJournalEntryTraceDto(
    Guid Id,
    string Reference,
    string SourceType,
    Guid? SourceId,
    DateTime EntryDate,
    string CurrencyCode,
    string Status,
    DateTime PostedAt,
    DateTime? ReversedAt,
    string? ReversalReason,
    IReadOnlyList<EmbeddedFinanceAdminJournalLineTraceDto> Lines);

public sealed record EmbeddedFinanceAdminAuditTimelineDto(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    string EntityName,
    string? EntityId,
    string? Reason,
    string? CorrelationId,
    DateTime OccurredAt);

public sealed record EmbeddedFinanceAdminActivityDetailDto(
    EmbeddedFinanceAdminActivityDto Activity,
    EmbeddedFinanceAdminApiContextDto? ApiContext,
    IReadOnlyList<EmbeddedFinanceAdminProviderTransactionTraceDto> ProviderTransactions,
    IReadOnlyList<EmbeddedFinanceAdminProviderRequestTraceDto> ProviderRequests,
    IReadOnlyList<EmbeddedFinanceAdminReservationTraceDto> Reservations,
    IReadOnlyList<EmbeddedFinanceAdminLedgerTransactionTraceDto> LedgerTransactions,
    IReadOnlyList<EmbeddedFinanceAdminJournalEntryTraceDto> JournalEntries,
    IReadOnlyList<EmbeddedFinanceAdminAuditTimelineDto> AuditTimeline);

public sealed record EmbeddedFinanceAdminExceptionDto(
    string SourceType,
    string Severity,
    Guid SourceId,
    Guid? BusinessProfileId,
    string? BusinessName,
    Guid? BusinessCustomerId,
    string? BusinessCustomerName,
    Guid? CollectionAccountId,
    string? Reference,
    string Status,
    string? ProviderCode,
    string? Reason,
    string? ActivityType,
    Guid? ActivityId,
    DateTime OccurredAt);
