using KorridorX.Dtos.Payments;
using KorridorX.Dtos.Wallets;

namespace KorridorX.Services.Wallets;

public interface IConsumerWalletFundingService
{
    Task<IReadOnlyList<CollectionPaymentMethodDto>> GetFundingMethodsAsync(
        Guid userId,
        Guid walletId,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> CreateCollectionAsync(
        Guid userId,
        Guid walletId,
        CreateConsumerWalletFundingCollectionRequestDto request,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> InitiateCollectionAsync(
        Guid userId,
        Guid walletId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> GetCollectionAsync(
        Guid userId,
        Guid walletId,
        Guid collectionId,
        CancellationToken ct = default);
}
