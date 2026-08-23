using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Marketplace;

public interface IMarketplaceOrderService
{
    Task<IReadOnlyList<MarketplacePairDto>> GetActivePairsAsync(CancellationToken ct = default);
    Task<OrderBookDto> GetOrderBookAsync(Guid marketplacePairId, int depth = 20, CancellationToken ct = default);
    Task<TradeOrderDto> CreateOrderAsync(Guid userId, CreateTradeOrderRequestDto request, CancellationToken ct = default);
    Task<TradeOrderDto> CancelOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<TradeOrderDto> GetOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<PagedResult<TradeOrderDto>> GetMyOrdersAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<TradeOrderDto> CreateOrderForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        Guid? actionedByUserId,
        CreateTradeOrderRequestDto request,
        CancellationToken ct = default);
    Task<TradeOrderDto> CancelOrderForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        Guid? actionedByUserId,
        Guid orderId,
        CancellationToken ct = default);
    Task<TradeOrderDto> GetOrderForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        Guid orderId,
        CancellationToken ct = default);
    Task<PagedResult<TradeOrderDto>> GetOrdersForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
