using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Marketplace;

public record MarketplacePairDto(
    Guid Id,
    string Code,
    string BaseAssetCode,
    string QuoteAssetCode,
    MarketplacePairStatus Status,
    decimal MinimumOrderQuantity,
    decimal? MaximumOrderQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement);

public record CreateTradeOrderRequestDto(
    Guid MarketplacePairId,
    TradeOrderSide Side,
    TradeOrderType OrderType,
    decimal Quantity,
    decimal? LimitPrice,
    TradeOrderTimeInForce TimeInForce = TradeOrderTimeInForce.GoodTillCancelled,
    DateTime? ExpiresAt = null);

public record TradeOrderDto(
    Guid Id,
    string Reference,
    Guid MarketplacePairId,
    string PairCode,
    TradeOrderSide Side,
    TradeOrderType OrderType,
    TradeOrderTimeInForce TimeInForce,
    TradeOrderStatus Status,
    decimal OriginalQuantity,
    decimal RemainingQuantity,
    decimal FilledQuantity,
    decimal? LimitPrice,
    decimal? AverageFillPrice,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ExpiresAt);

public record OrderBookLevelDto(decimal Price, decimal Quantity, int OrderCount);

public record OrderBookDto(
    Guid MarketplacePairId,
    string PairCode,
    IReadOnlyList<OrderBookLevelDto> Bids,
    IReadOnlyList<OrderBookLevelDto> Asks,
    DateTime AsOf);

public record TradeMatchDto(
    Guid Id,
    string Reference,
    Guid BuyOrderId,
    Guid SellOrderId,
    decimal Price,
    decimal BaseQuantity,
    decimal QuoteQuantity,
    TradeMatchStatus Status,
    DateTime MatchedAt);

public record TradeDto(
    Guid Id,
    string Reference,
    Guid TradeMatchId,
    Guid MarketplacePairId,
    decimal Price,
    decimal BaseQuantity,
    decimal QuoteQuantity,
    TradeStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record CreateMarketplacePairRequestDto(
    string BaseAssetCode,
    string QuoteAssetCode,
    decimal MinimumOrderQuantity,
    decimal? MaximumOrderQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement,
    MarketplacePairStatus Status = MarketplacePairStatus.Paused);

public record UpdateMarketplacePairRequestDto(
    decimal MinimumOrderQuantity,
    decimal? MaximumOrderQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement);

public record SetMarketplacePairStatusRequestDto(
    MarketplacePairStatus Status);

public record TradeHistoryDto(
    Guid Id,
    string Reference,
    Guid TradeMatchId,
    Guid MarketplacePairId,
    string PairCode,
    TradeOrderSide Side,
    decimal Price,
    decimal BaseQuantity,
    decimal QuoteQuantity,
    TradeStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record MarketplaceOperationFailureDto(
    Guid EntityId,
    string Error);

public record MarketplaceMaintenanceResultDto(
    int Candidates,
    int ExpiredOrders,
    decimal ReleasedAmount,
    IReadOnlyList<MarketplaceOperationFailureDto> Failures);

public record MarketplaceRecoveryResultDto(
    int Candidates,
    int RecoveredMatches,
    IReadOnlyList<MarketplaceOperationFailureDto> Failures);


public record CreateBusinessTradingRfqRequestDto(
    Guid MarketplacePairId,
    Guid CounterpartyBusinessCustomerId,
    TradeOrderSide Side,
    decimal Quantity,
    DateTime? ExpiresAt = null);

public record CreateBusinessTradingRfqQuoteRequestDto(
    decimal Price,
    DateTime? ExpiresAt = null);

public record BusinessTradingRfqQuoteDto(
    Guid Id,
    string Reference,
    decimal Price,
    BusinessTradingRfqQuoteStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public record BusinessTradingRfqDto(
    Guid Id,
    string Reference,
    Guid MarketplacePairId,
    string PairCode,
    FinancialAccountOwnerType RequesterOwnerType,
    Guid RequesterOwnerId,
    FinancialAccountOwnerType CounterpartyOwnerType,
    Guid CounterpartyOwnerId,
    TradeOrderSide Side,
    decimal Quantity,
    BusinessTradingRfqStatus Status,
    DateTime ExpiresAt,
    Guid? AcceptedQuoteId,
    Guid? TradeMatchId,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    IReadOnlyList<BusinessTradingRfqQuoteDto> Quotes);
