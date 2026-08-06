using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Infrastructure;

namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessPaymentBatchService
{
    Task<BusinessPaymentBatchDetailsDto> ImportAsync(
        Guid userId,
        string fileName,
        Stream csvStream,
        ImportBusinessPaymentBatchRequestDto request,
        CancellationToken ct = default);

    Task<PagedResult<BusinessPaymentBatchDto>> GetBatchesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<BusinessPaymentBatchDetailsDto> GetBatchAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default);

    Task<BusinessPaymentBatchDetailsDto> SubmitAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default);

    Task<BusinessPaymentBatchDetailsDto> ApproveAsync(
        Guid userId,
        Guid batchId,
        BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct = default);

    Task<BusinessPaymentBatchDetailsDto> RejectAsync(
        Guid userId,
        Guid batchId,
        BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct = default);
}
