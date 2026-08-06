using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Payments;

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionPaymentMethodDto>> GetAvailablePaymentMethodsAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> CreateCollectionAsync(
        Guid userId,
        Guid transferId,
        CreateCollectionRequestDto request,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> InitiateCollectionAsync(
        Guid userId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> GetCollectionByIdAsync(
        Guid userId,
        Guid collectionId,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> GetTransferCollectionAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<PagedResult<CollectionDto>> GetMyCollectionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
