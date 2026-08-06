using KorridorX.Dtos.Transfers;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Transfers;

public interface ITransferService
{
    Task<TransferDetailsDto> CreateTransferAsync(
        Guid userId,
        CreateTransferRequestDto request,
        CancellationToken ct = default);

    Task<TransferDetailsDto> GetTransferByIdAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<PagedResult<TransferDto>> GetMyTransfersAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<TransferDetailsDto> CancelTransferAsync(
        Guid userId,
        Guid transferId,
        CancelTransferRequestDto request,
        CancellationToken ct = default);
}