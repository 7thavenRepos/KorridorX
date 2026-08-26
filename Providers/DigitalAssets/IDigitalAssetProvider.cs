namespace KorridorX.Providers.DigitalAssets;

public interface IDigitalAssetProvider
{
    string ProviderCode { get; }
    bool Supports(string assetCode, string networkCode);
    Task<DigitalAssetDepositAddressResult> CreateDepositAddressAsync(DigitalAssetDepositAddressRequest request, CancellationToken ct = default);
    Task<DigitalAssetWithdrawalSubmissionResult> SubmitWithdrawalAsync(DigitalAssetWithdrawalSubmissionRequest request, CancellationToken ct = default);
}

public sealed record DigitalAssetDepositAddressRequest(Guid BusinessProfileId, Guid? BusinessCustomerId, Guid FinancialAccountId, string AssetCode, string NetworkCode);
public sealed record DigitalAssetDepositAddressResult(string ProviderAddressId, string Address, string? DestinationTag, string? ProviderReference = null);
public sealed record DigitalAssetWithdrawalSubmissionRequest(Guid WithdrawalId, string AssetCode, string NetworkCode, string Address, string? DestinationTag, decimal Amount, string ExternalReference);
public sealed record DigitalAssetWithdrawalSubmissionResult(string ProviderTransactionId, string? ProviderReference, string? TransactionHash, string ProviderStatus);