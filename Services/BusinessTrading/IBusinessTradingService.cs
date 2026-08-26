using KorridorX.Dtos.BusinessTrading;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;

namespace KorridorX.Services.BusinessTrading;

public interface IBusinessTradingService
{
    Task<BusinessTradingWorkspaceDto> GetWorkspaceAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessTradingBalanceDto>> GetBalancesAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<MarketplacePairDto>> GetMarketplacePairsAsync(Guid userId, CancellationToken ct = default);
    Task<OrderBookDto> GetOrderBookAsync(Guid userId, Guid pairId, int depth = 20, CancellationToken ct = default);
    Task<TradeOrderDto> CreateMarketplaceOrderAsync(Guid userId, CreateTradeOrderRequestDto request, CancellationToken ct = default);
    Task<TradeOrderDto> GetMarketplaceOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<TradeOrderDto> CancelMarketplaceOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<PagedResult<TradeOrderDto>> GetMarketplaceOrdersAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PagedResult<TradeHistoryDto>> GetMarketplaceTradesAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task<IReadOnlyList<InstantPairDto>> GetInstantPairsAsync(Guid userId, CancellationToken ct = default);
    Task<InstantQuoteDto> CreateInstantQuoteAsync(Guid userId, CreateInstantQuoteRequestDto request, CancellationToken ct = default);
    Task<InstantTradeDto> ExecuteInstantQuoteAsync(Guid userId, Guid quoteId, CancellationToken ct = default);
    Task<PagedResult<InstantTradeDto>> GetInstantTradesAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessTradingCounterpartyDto>> SearchCounterpartiesAsync(
        Guid userId,
        string? search = null,
        Guid? marketplacePairId = null,
        int take = 20,
        CancellationToken ct = default);

    Task<BusinessTradingRfqDto> CreateRfqAsync(
        Guid userId,
        CreateBusinessProfileTradingRfqRequestDto request,
        CancellationToken ct = default);

    Task<BusinessTradingRfqDto> QuoteRfqAsync(
        Guid userId,
        Guid rfqId,
        CreateBusinessProfileTradingRfqQuoteRequestDto request,
        CancellationToken ct = default);

    Task<BusinessTradingRfqDto> AcceptRfqQuoteAsync(
        Guid userId,
        Guid rfqId,
        Guid quoteId,
        CancellationToken ct = default);

    Task<BusinessTradingRfqDto> CancelRfqAsync(
        Guid userId,
        Guid rfqId,
        CancellationToken ct = default);

    Task<BusinessTradingRfqDto> GetRfqAsync(
        Guid userId,
        Guid rfqId,
        CancellationToken ct = default);

    Task<PagedResult<BusinessTradingRfqDto>> GetRfqsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}