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
