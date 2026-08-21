using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public record CreateApiApplicationRequestDto(string Name, string? Description, EmbeddedFinanceScope Scopes, IReadOnlyList<string>? AllowedIpRanges);
public record ApiApplicationDto(Guid Id, string Name, string? Description, EmbeddedFinanceScope Scopes, ApiApplicationStatus Status, IReadOnlyList<string> AllowedIpRanges, DateTime CreatedAt, DateTime? LastAuthenticatedAt);
public record CreateApiCredentialRequestDto(string Name, DateTime? ExpiresAt);
public record ApiCredentialCreatedDto(Guid Id, Guid ApiApplicationId, string Name, string KeyId, string ApiKey, string SecretLastFour, ApiCredentialStatus Status, DateTime? ExpiresAt, DateTime CreatedAt);
public record ApiCredentialDto(Guid Id, Guid ApiApplicationId, string Name, string KeyId, string SecretLastFour, ApiCredentialStatus Status, DateTime? ExpiresAt, DateTime? LastUsedAt, string? LastUsedIpAddress, DateTime CreatedAt);
public record CreateBusinessCustomerRequestDto(string ExternalReference, string DisplayName, string? Email, string? PhoneNumber, string CountryCode, string? MetadataJson);
public record BusinessCustomerDto(Guid Id, string ExternalReference, string DisplayName, string? Email, string? PhoneNumber, string CountryCode, BusinessCustomerStatus Status, string? MetadataJson, DateTime CreatedAt, DateTime? LastUpdatedAt);
public record CreateCollectionAccountRequestDto(string ExternalReference, string AssetCode);
public record CollectionAccountDto(Guid Id, Guid BusinessCustomerId, string ExternalReference, string AssetCode, Guid FinancialAccountId, CollectionAccountStatus Status, decimal SettledBalance, decimal AvailableBalance, decimal HeldBalance, DateTime CreatedAt);


public record CreateBusinessWebhookEndpointRequestDto(
    Guid ApiApplicationId,
    string Url,
    IReadOnlyList<string> EventTypes,
    int MaxAttempts = 8);

public record BusinessWebhookEndpointCreatedDto(
    Guid Id,
    Guid ApiApplicationId,
    string Url,
    IReadOnlyList<string> EventTypes,
    BusinessWebhookEndpointStatus Status,
    string SigningSecret,
    string SigningSecretLastFour,
    int MaxAttempts,
    DateTime CreatedAt);

public record BusinessWebhookEndpointDto(
    Guid Id,
    Guid ApiApplicationId,
    string Url,
    IReadOnlyList<string> EventTypes,
    BusinessWebhookEndpointStatus Status,
    string SigningSecretLastFour,
    int MaxAttempts,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record RotateBusinessWebhookSecretDto(
    Guid EndpointId,
    string SigningSecret,
    string SigningSecretLastFour);

public record ProvisionCollectionAccountRequestDto(string ProviderCode);

public record ProviderAccountMappingDto(
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

public record BusinessWebhookDeliveryDto(
    Guid Id, Guid EndpointId, Guid EventRowId, string EventId, string EventType,
    BusinessWebhookDeliveryStatus Status, int AttemptCount, DateTime? NextAttemptAt,
    DateTime? LastAttemptAt, DateTime? DeliveredAt, DateTime? DeadLetteredAt,
    int? LastResponseStatusCode, string? ErrorMessage, DateTime CreatedAt);
