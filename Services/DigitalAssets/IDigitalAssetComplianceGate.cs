namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetComplianceGate
{
    Task EnsureDepositAllowedAsync(Guid businessProfileId, Guid businessCustomerId, string assetCode, string networkCode, decimal amount, string? sourceAddress, CancellationToken ct = default);
    Task EnsureWithdrawalAllowedAsync(Guid businessProfileId, Guid businessCustomerId, string assetCode, string networkCode, decimal amount, string destinationAddress, CancellationToken ct = default);
}
