using KorridorX.Dtos.BusinessDigitalAssets;
using KorridorX.Dtos.DigitalAssets;

namespace KorridorX.Services.BusinessDigitalAssets;

public interface IBusinessDigitalAssetService
{
    Task<BusinessDigitalAssetWorkspaceDto> GetWorkspaceAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetDepositAddressDto>> GetDepositAddressesAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<DigitalAssetDepositAddressDto> CreateDepositAddressAsync(
        Guid userId,
        CreateDigitalAssetDepositAddressRequestDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetDepositIntentDto>> GetDepositIntentsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<DigitalAssetDepositIntentDto> CreateDepositIntentAsync(
        Guid userId,
        CreateDigitalAssetDepositIntentRequestDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetWithdrawalDestinationDto>> GetWithdrawalDestinationsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<DigitalAssetWithdrawalDestinationDto> CreateWithdrawalDestinationAsync(
        Guid userId,
        CreateDigitalAssetWithdrawalDestinationRequestDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<DigitalAssetWithdrawalDto>> GetWithdrawalsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<DigitalAssetWithdrawalDto> CreateWithdrawalAsync(
        Guid userId,
        CreateDigitalAssetWithdrawalRequestDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<BusinessDigitalAssetActivityDto>> GetActivityAsync(
        Guid userId,
        int take = 100,
        CancellationToken ct = default);
}