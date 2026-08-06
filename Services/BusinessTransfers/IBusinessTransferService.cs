using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Dtos.Fx;
using KorridorX.Infrastructure;

namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessTransferService
{
    Task<TransferQuoteDto> CreateQuoteAsync(Guid userId, CreateBusinessTransferQuoteRequestDto request, CancellationToken ct = default);
    Task<TransferQuoteDto> GetQuoteAsync(Guid userId, Guid quoteId, CancellationToken ct = default);
    Task<BusinessTransferDetailsDto> CreateTransferAsync(Guid userId, CreateBusinessTransferRequestDto request, CancellationToken ct = default);
    Task<BusinessTransferDetailsDto> GetTransferAsync(Guid userId, Guid transferId, CancellationToken ct = default);
    Task<PagedResult<BusinessTransferDto>> GetTransfersAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<BusinessTransferDetailsDto> ApproveAsync(Guid userId, Guid transferId, BusinessTransferDecisionRequestDto request, CancellationToken ct = default);
    Task<BusinessTransferDetailsDto> RejectAsync(Guid userId, Guid transferId, BusinessTransferDecisionRequestDto request, CancellationToken ct = default);
    Task<BusinessTransferReportDto> GetReportAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct = default);
}
