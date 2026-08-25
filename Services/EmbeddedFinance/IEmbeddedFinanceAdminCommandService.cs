using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Enums;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinanceAdminCommandService
{
    Task<EmbeddedFinanceAdminActionResultDto> SetApplicationStatusAsync(
        Guid actorUserId,
        Guid apiApplicationId,
        ApiApplicationStatus status,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminActionResultDto> RevokeCredentialAsync(
        Guid actorUserId,
        Guid apiApplicationId,
        Guid credentialId,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminActionResultDto> SetCustomerStatusAsync(
        Guid actorUserId,
        Guid businessCustomerId,
        BusinessCustomerStatus status,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminCollectionAccountActionResultDto> SetCollectionAccountStatusAsync(
        Guid actorUserId,
        Guid collectionAccountId,
        CollectionAccountStatus status,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminProvisioningRetryResultDto> RetryProviderMappingAsync(
        Guid actorUserId,
        Guid collectionAccountId,
        Guid providerMappingId,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminActionResultDto> SetWebhookEndpointStatusAsync(
        Guid actorUserId,
        Guid endpointId,
        bool enabled,
        string reason,
        CancellationToken ct = default);

    Task<EmbeddedFinanceAdminActionResultDto> RetryWebhookDeliveryAsync(
        Guid actorUserId,
        Guid deliveryId,
        string reason,
        CancellationToken ct = default);
}
