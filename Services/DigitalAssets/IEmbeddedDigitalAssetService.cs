using KorridorX.Dtos.DigitalAssets;

namespace KorridorX.Services.DigitalAssets;

public interface IEmbeddedDigitalAssetService
{
    Task<IReadOnlyList<DigitalAssetDepositAddressDto>> GetDepositAddressesAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<DigitalAssetDepositAddressDto> CreateDepositAddressAsync(Guid businessCustomerId, CreateDigitalAssetDepositAddressRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<DigitalAssetDepositIntentDto>> GetDepositIntentsAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<DigitalAssetDepositIntentDto> CreateDepositIntentAsync(Guid businessCustomerId, CreateDigitalAssetDepositIntentRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<DigitalAssetWithdrawalDestinationDto>> GetWithdrawalDestinationsAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<DigitalAssetWithdrawalDestinationDto> CreateWithdrawalDestinationAsync(Guid businessCustomerId, CreateDigitalAssetWithdrawalDestinationRequestDto request, CancellationToken ct = default);
    Task<DigitalAssetWithdrawalDto> CreateWithdrawalAsync(Guid businessCustomerId, CreateDigitalAssetWithdrawalRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<DigitalAssetWithdrawalDto>> GetWithdrawalsAsync(Guid businessCustomerId, CancellationToken ct = default);
}

public interface IDigitalAssetSettlementService
{
    Task ProcessInboundAsync(DigitalAssetInboundNotification notification, CancellationToken ct = default);
    Task ProcessOutboundAsync(DigitalAssetOutboundNotification notification, CancellationToken ct = default);
}

public interface IDigitalAssetDepositIntentSettlementService
{
    Task<bool> TryProcessProviderCollectionAsync(
        string providerCode,
        string providerCollectionId,
        string? providerReference,
        string providerStatus,
        string? assetCode,
        decimal? amount,
        string rawPayload,
        DateTime occurredAt,
        CancellationToken ct = default);
}
