using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
namespace KorridorX.Services.EmbeddedFinance;
public interface IEmbeddedFinanceCustomerService
{
    Task<PagedResult<BusinessCustomerDto>> GetCustomersAsync(int page, int pageSize, CancellationToken ct = default);
    Task<BusinessCustomerDto> CreateCustomerAsync(CreateBusinessCustomerRequestDto request, string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionAccountDto>> GetAccountsAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<CollectionAccountDto> CreateAccountAsync(Guid businessCustomerId, CreateCollectionAccountRequestDto request, string idempotencyKey, CancellationToken ct = default);
}
