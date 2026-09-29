using KorridorX.Dtos.Wallets;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Wallets;

public interface IConsumerTransferFundingService
{
    Task<ConsumerTransferFundingDto> PrepareTransferAsync(
        Guid userId,
        Transfer transfer,
        CancellationToken ct = default);

    Task<ConsumerTransferFundingDto> GetFundingAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task ApplySuccessfulCollectionAsync(
        Collection collection,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default);

    bool CaptureTransferReservation(
        Transfer transfer,
        Guid? actionedByUserId,
        string source);

    Task<bool> ReleaseActiveTransferReservationAsync(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default);

    bool ReleaseTransferReservation(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source);
}
