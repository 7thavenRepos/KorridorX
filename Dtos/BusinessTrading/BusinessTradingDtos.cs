using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessTrading;

public sealed record BusinessTradingBalanceDto(
    Guid FinancialAccountId,
    string AssetCode,
    string AssetName,
    AssetType AssetType,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record BusinessTradingCounterpartyDto(
    Guid BusinessProfileId,
    string BusinessName,
    string? TradingName,
    string CountryCode);

public sealed record CreateBusinessProfileTradingRfqRequestDto(
    Guid MarketplacePairId,
    Guid CounterpartyBusinessProfileId,
    TradeOrderSide Side,
    decimal Quantity,
    DateTime? ExpiresAt = null);

public sealed record CreateBusinessProfileTradingRfqQuoteRequestDto(
    decimal Price,
    DateTime? ExpiresAt = null);

public sealed record BusinessTradingWorkspaceDto(
    IReadOnlyList<BusinessTradingBalanceDto> Balances,
    IReadOnlyList<MarketplacePairDto> MarketplacePairs,
    IReadOnlyList<InstantPairDto> InstantPairs);