using KorridorX.Dtos.Payments;

namespace KorridorX.Services.Payments;

public interface IRefundService
{
    Task<RefundDto> InitiateAsync(
        Guid collectionId,
        string? reason,
        Guid changedByUserId,
        CancellationToken ct = default);

    Task<RefundDto> RefreshAsync(
        Guid collectionId,
        Guid changedByUserId,
        CancellationToken ct = default);

    Task<RefundDto> GetAsync(
        Guid collectionId,
        CancellationToken ct = default);
}
