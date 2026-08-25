using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinanceAdminQueryService
{
    Task<EmbeddedFinanceAdminOverviewDto> GetOverviewAsync(
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminBusinessDto>> GetBusinessesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminApplicationDto>> GetApplicationsAsync(
        Guid? businessProfileId,
        ApiApplicationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmbeddedFinanceAdminCredentialDto>> GetCredentialsAsync(
        Guid apiApplicationId,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminCustomerDto>> GetCustomersAsync(
        Guid? businessProfileId,
        BusinessCustomerStatus? status,
        string? countryCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminCollectionAccountDto>> GetAccountsAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        CollectionAccountStatus? status,
        string? assetCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmbeddedFinanceAdminProviderMappingDto>> GetProviderMappingsAsync(
        Guid collectionAccountId,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminActivityDto>> GetActivityAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        Guid? collectionAccountId,
        string? activityType,
        string? assetCode,
        string? providerCode,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken ct = default,
        Guid? activityId = null);

    Task<EmbeddedFinanceAdminActivityDetailDto> GetActivityDetailAsync(
        string activityType,
        Guid activityId,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminExceptionDto>> GetExceptionsAsync(
        Guid? businessProfileId,
        string? sourceType,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<byte[]> ExportActivityCsvAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        Guid? collectionAccountId,
        string? activityType,
        string? assetCode,
        string? providerCode,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminWebhookEndpointDto>> GetWebhookEndpointsAsync(
        Guid? businessProfileId,
        BusinessWebhookEndpointStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedFinanceAdminWebhookDeliveryDto>> GetWebhookDeliveriesAsync(
        Guid? businessProfileId,
        BusinessWebhookDeliveryStatus? status,
        string? eventType,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
