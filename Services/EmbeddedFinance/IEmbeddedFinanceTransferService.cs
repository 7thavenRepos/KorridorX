using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Fx;
using KorridorX.Infrastructure;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinanceTransferService
{
    Task<TransferQuoteDto> CreateQuoteAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedTransferQuoteRequestDto request,
        CancellationToken ct = default);

    Task<TransferQuoteDto> GetQuoteAsync(
        Guid businessCustomerId,
        Guid quoteId,
        CancellationToken ct = default);

    Task<PagedResult<EmbeddedTransferDto>> GetTransfersAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<EmbeddedTransferDto> GetTransferAsync(
        Guid businessCustomerId,
        Guid transferId,
        CancellationToken ct = default);

    Task<EmbeddedTransferCreateResultDto> CreateTransferAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedTransferRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);
}
