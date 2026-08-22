using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedTradingService
{
    Task<IReadOnlyList<EmbeddedTradingBalanceDto>> GetBalancesAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<IReadOnlyList<MarketplacePairDto>> GetMarketplacePairsAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<OrderBookDto> GetOrderBookAsync(Guid businessCustomerId, Guid pairId, int depth, CancellationToken ct = default);
    Task<TradeOrderDto> CreateMarketplaceOrderAsync(Guid businessCustomerId, CreateTradeOrderRequestDto request, CancellationToken ct = default);
    Task<TradeOrderDto> GetMarketplaceOrderAsync(Guid businessCustomerId, Guid orderId, CancellationToken ct = default);
    Task<TradeOrderDto> CancelMarketplaceOrderAsync(Guid businessCustomerId, Guid orderId, CancellationToken ct = default);
    Task<PagedResult<TradeOrderDto>> GetMarketplaceOrdersAsync(Guid businessCustomerId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<TradeHistoryDto>> GetMarketplaceTradesAsync(Guid businessCustomerId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<InstantPairDto>> GetInstantPairsAsync(Guid businessCustomerId, CancellationToken ct = default);
    Task<InstantQuoteDto> CreateInstantQuoteAsync(Guid businessCustomerId, CreateInstantQuoteRequestDto request, CancellationToken ct = default);
    Task<InstantTradeDto> ExecuteInstantQuoteAsync(Guid businessCustomerId, Guid quoteId, CancellationToken ct = default);
    Task<PagedResult<InstantTradeDto>> GetInstantTradesAsync(Guid businessCustomerId, int page, int pageSize, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> CreateRfqAsync(Guid businessCustomerId, CreateBusinessTradingRfqRequestDto request, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> QuoteRfqAsync(Guid businessCustomerId, Guid rfqId, CreateBusinessTradingRfqQuoteRequestDto request, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> AcceptRfqQuoteAsync(Guid businessCustomerId, Guid rfqId, Guid quoteId, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> CancelRfqAsync(Guid businessCustomerId, Guid rfqId, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> GetRfqAsync(Guid businessCustomerId, Guid rfqId, CancellationToken ct = default);
    Task<PagedResult<BusinessTradingRfqDto>> GetRfqsAsync(Guid businessCustomerId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
