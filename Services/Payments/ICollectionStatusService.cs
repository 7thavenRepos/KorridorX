using KorridorX.Models.Enums;
using KorridorX.Models.Payments;

namespace KorridorX.Services.Payments;

public interface ICollectionStatusService
{
    bool CanTransition(
        CollectionStatus currentStatus,
        CollectionStatus newStatus);

    bool ApplyTransition(
        Collection collection,
        CollectionStatus newStatus,
        CollectionStatusTransitionContext context,
        CollectionAttempt? attempt = null);

    Task<bool> ApplyTransitionAsync(
        Collection collection,
        CollectionStatus newStatus,
        CollectionStatusTransitionContext context,
        CollectionAttempt? attempt = null,
        CancellationToken ct = default);
}
