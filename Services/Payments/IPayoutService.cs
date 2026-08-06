using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Payments;

public interface IPayoutService
{
    Task<PayoutDetailsDto> DispatchForTransferAsync(
        Guid transferId,
        string source,
        Guid? changedByUserId = null,
        CancellationToken ct = default);

    Task<int> DispatchPendingAsync(
        int batchSize,
        CancellationToken ct = default);

    Task<PayoutDetailsDto> RetryFailedAsync(
        Guid payoutId,
        Guid changedByUserId,
        CancellationToken ct = default);

    Task<PayoutDetailsDto> GetPayoutByIdAsync(
        Guid userId,
        Guid payoutId,
        CancellationToken ct = default);

    Task<PayoutDetailsDto> GetTransferPayoutAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<PagedResult<PayoutDto>> GetMyPayoutsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
