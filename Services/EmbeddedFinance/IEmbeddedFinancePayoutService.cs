using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinancePayoutService
{
    Task<PagedResult<EmbeddedPayoutDto>> GetPayoutsAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<EmbeddedPayoutDto> GetPayoutAsync(
        Guid businessCustomerId,
        Guid payoutId,
        CancellationToken ct = default);

    Task<EmbeddedPayoutDto> CreatePayoutAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedPayoutRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);
}
