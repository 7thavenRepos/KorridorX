using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.Enums;

namespace KorridorX.Providers.DigitalAssets;

public interface IDigitalAssetWebhookProvider
{
    bool VerifyWebhookSignature(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        string webhookSecret);

    Task<DigitalAssetProviderWebhookEvent> ParseWebhookAsync(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default);
}

public interface IDigitalAssetBalanceProvider
{
    Task<IReadOnlyList<DigitalAssetProviderBalanceSnapshot>> GetBalancesAsync(
        CancellationToken ct = default);
}

public interface IDigitalAssetCollectionProvider
{
    Task<DigitalAssetCollectionIntentResult> CreateCollectionIntentAsync(
        DigitalAssetCollectionIntentRequest request,
        CancellationToken ct = default);
}

public sealed record DigitalAssetCollectionIntentRequest(
    Guid DepositIntentId,
    Guid BusinessProfileId,
    Guid BusinessCustomerId,
    Guid FinancialAccountId,
    string AssetCode,
    string NetworkCode,
    decimal Amount);

public sealed record DigitalAssetCollectionIntentResult(
    string ProviderWalletId,
    string ProviderCollectionId,
    string? ProviderReference,
    string Address,
    string ProviderNetworkCode,
    decimal Amount,
    string ProviderStatus,
    DateTime? ExpiresAt);

public interface IDigitalAssetHealthProvider
{
    Task<DigitalAssetProviderHealthResult> CheckHealthAsync(
        CancellationToken ct = default);
}

public interface IDigitalAssetAddressRiskProvider
{
    string RiskProviderCode { get; }

    Task<DigitalAssetAddressRiskResult> ScreenAsync(
        string assetCode,
        string networkCode,
        string address,
        DigitalAssetAddressScreeningDirection direction,
        CancellationToken ct = default);
}

public sealed record DigitalAssetProviderWebhookEvent(
    string ProviderEventId,
    string EventType,
    DigitalAssetInboundNotification? Inbound,
    DigitalAssetOutboundNotification? Outbound);

public sealed record DigitalAssetProviderBalanceSnapshot(
    string ProviderWalletId,
    string AssetCode,
    string? NetworkCode,
    decimal Balance,
    bool IsActive,
    string? ProviderReference = null);

public sealed record DigitalAssetProviderHealthResult(
    bool IsHealthy,
    string Message);

public sealed record DigitalAssetAddressRiskResult(
    decimal RiskScore,
    DigitalAssetAddressRiskLevel RiskLevel,
    bool IsBlocking,
    IReadOnlyList<string> Reasons,
    string? ProviderReference = null,
    string? RawResultJson = null,
    DateTime? ExpiresAt = null);
