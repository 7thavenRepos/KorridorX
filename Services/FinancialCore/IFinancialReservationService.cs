using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Services.FinancialCore;

public interface IFinancialReservationService
{
    Task<FinancialReservation> ReserveAsync(
        Guid financialAccountId,
        FinancialReservationType type,
        string relatedEntityType,
        Guid relatedEntityId,
        decimal amount,
        Guid? actionedByUserId,
        string? contextEntityType = null,
        Guid? contextEntityId = null,
        CancellationToken ct = default);

    Task<FinancialReservation> ReleaseAsync(
        Guid reservationId,
        decimal amount,
        string reason,
        Guid? actionedByUserId,
        CancellationToken ct = default);
}
