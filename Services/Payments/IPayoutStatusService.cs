using KorridorX.Models.Enums;
using KorridorX.Models.Payments;

namespace KorridorX.Services.Payments;

public interface IPayoutStatusService
{
    bool CanTransition(PayoutStatus currentStatus, PayoutStatus newStatus);

    bool ApplyTransition(
        Payout payout,
        PayoutStatus newStatus,
        PayoutStatusTransitionContext context,
        PayoutAttempt? attempt = null);
}
