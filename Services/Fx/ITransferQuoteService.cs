using KorridorX.Dtos.Fx;

namespace KorridorX.Services.Fx;

public interface ITransferQuoteService
{
    Task<TransferQuoteDto> CreateQuoteAsync(
        Guid userId,
        CreateTransferQuoteRequestDto request,
        CancellationToken ct = default);

    Task<TransferQuoteDto> GetQuoteByIdAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct = default);
}