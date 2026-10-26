using KorridorX.Dtos.AccountClosure;
using KorridorX.Infrastructure;

namespace KorridorX.Services.AccountClosure;

public interface IAccountClosureService
{
    Task<AccountClosureOverviewDto> GetPersonalAsync(Guid userId, CancellationToken ct);
    Task<AccountClosureOverviewDto> RequestPersonalAsync(Guid userId, CancellationToken ct);
    Task<AccountClosureOverviewDto> WithdrawPersonalAsync(Guid userId, CancellationToken ct);
    Task<AccountClosureOverviewDto> GetBusinessAsync(Guid userId, CancellationToken ct);
    Task<AccountClosureOverviewDto> RequestBusinessAsync(Guid userId, CancellationToken ct);
    Task<AccountClosureOverviewDto> WithdrawBusinessAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<AccountClosureQueueRowDto>> ListForOperationsAsync(
        int page, int pageSize, CancellationToken ct);
}
