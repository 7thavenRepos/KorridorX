namespace KorridorX.Dtos.Providers;

public record SyncProviderCustomerRequestDto
(
    string? IdType,
    string? IdNumber,
    DateTime? IdIssueDate,
    DateTime? IdExpiryDate
);

public record ProviderCustomerDto
(
    Guid Id,
    Guid CustomerProfileId,
    string ProviderCode,
    string ProviderCustomerId,
    string? ProviderStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt
);

public record ProviderHealthDto
(
    string Provider,
    bool IsAvailable,
    DateTime CheckedAt
);
