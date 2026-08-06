using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Transfers;

public interface ITransferStatusService
{
    bool CanTransition(
        TransferStatus currentStatus,
        TransferStatus newStatus);

    bool ApplyTransition(
        Transfer transfer,
        TransferStatus newStatus,
        TransferStatusTransitionContext context);
}
