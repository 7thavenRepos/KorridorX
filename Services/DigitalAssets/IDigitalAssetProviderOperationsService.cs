using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetProviderOperationsService
{
    Task<IReadOnlyList<DigitalAssetProviderConfigurationDto>> GetConfigurationsAsync(
        CancellationToken ct = default);

    Task<DigitalAssetProviderConfigurationDto> UpsertConfigurationAsync(
        Guid userId,
        UpsertDigitalAssetProviderConfigurationRequestDto request,
        CancellationToken ct = default);

    Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        CancellationToken ct = default);

    Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        Guid userId,
        string reason,
        CancellationToken ct = default);

    Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        CancellationToken ct = default);

    Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        Guid userId,
        string reason,
        CancellationToken ct = default);

    Task<PagedResult<DigitalAssetProviderBalanceDto>> GetBalancesAsync(
        string? providerCode,
        string? assetCode,
        string? networkCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<DigitalAssetNetworkTransactionDto>> GetNetworkTransactionsAsync(
        string? providerCode,
        string? assetCode,
        string? networkCode,
        DigitalAssetTransactionDirection? direction,
        DigitalAssetTransactionStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<DigitalAssetNetworkTransactionDto> GetNetworkTransactionAsync(
        Guid transactionId,
        CancellationToken ct = default);

    Task<PagedResult<DigitalAssetWebhookReceiptDto>> GetWebhookReceiptsAsync(
        string? providerCode,
        DigitalAssetWebhookReceiptStatus? status,
        string? eventType,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<DigitalAssetWebhookReceiptDto> GetWebhookReceiptAsync(
        Guid webhookReceiptId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetNetworkTransactionDto>> GetReconciliationExceptionsAsync(
        int take = 100,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetFeeExceptionDto>> GetFeeExceptionsAsync(
        decimal minimumAbsoluteVariance = 0.00000001m,
        int take = 100,
        CancellationToken ct = default);
}