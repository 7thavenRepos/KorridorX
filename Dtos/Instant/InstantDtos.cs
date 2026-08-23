using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Instant;

public record InstantPairDto(
    Guid Id,
    string Code,
    string SourceAssetCode,
    string DestinationAssetCode,
    InstantPairStatus Status,
    decimal MinimumSourceAmount,
    decimal? MaximumSourceAmount,
    decimal SourceAmountIncrement,
    int QuoteValiditySeconds);

public record CreateInstantPairRequestDto(
    string SourceAssetCode,
    string DestinationAssetCode,
    Guid HouseSourceFinancialAccountId,
    Guid HouseDestinationFinancialAccountId,
    decimal MinimumSourceAmount,
    decimal? MaximumSourceAmount,
    decimal SourceAmountIncrement,
    int QuoteValiditySeconds = 30,
    InstantPairStatus Status = InstantPairStatus.Paused);

public record UpdateInstantPairRequestDto(
    Guid HouseSourceFinancialAccountId,
    Guid HouseDestinationFinancialAccountId,
    decimal MinimumSourceAmount,
    decimal? MaximumSourceAmount,
    decimal SourceAmountIncrement,
    int QuoteValiditySeconds);

public record SetInstantPairStatusRequestDto(InstantPairStatus Status);

public record CreateInstantQuoteRequestDto(
    Guid InstantPairId,
    decimal SourceAmount);

public record InstantQuoteDto(
    Guid Id,
    string Reference,
    Guid InstantPairId,
    string PairCode,
    string SourceAssetCode,
    string DestinationAssetCode,
    decimal SourceAmount,
    decimal DestinationAmount,
    decimal CustomerRate,
    DateTime ExpiresAt,
    InstantQuoteStatus Status,
    decimal? BaseCustomerRate = null,
    decimal? BaseDestinationAmount = null,
    decimal? BusinessRevenueAmount = null);

public record ExecuteInstantQuoteRequestDto(Guid QuoteId);

public record InstantTradeDto(
    Guid Id,
    string Reference,
    Guid InstantQuoteId,
    Guid InstantPairId,
    string PairCode,
    string SourceAssetCode,
    string DestinationAssetCode,
    decimal SourceAmount,
    decimal DestinationAmount,
    decimal CustomerRate,
    InstantTradeStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    decimal? BaseCustomerRate = null,
    decimal? BaseDestinationAmount = null,
    decimal? BusinessRevenueAmount = null);


public record InstantLiquidityDto(
    Guid InstantPairId,
    string PairCode,
    Guid HouseSourceFinancialAccountId,
    string SourceAssetCode,
    decimal HouseSourceAvailableBalance,
    decimal HouseSourceHeldBalance,
    decimal HouseSourceSettledBalance,
    Guid HouseDestinationFinancialAccountId,
    string DestinationAssetCode,
    decimal HouseDestinationAvailableBalance,
    decimal HouseDestinationHeldBalance,
    decimal HouseDestinationSettledBalance,
    InstantPairStatus PairStatus);

public record InstantQuoteAdminDto(
    Guid Id,
    string Reference,
    Guid UserId,
    Guid InstantPairId,
    string PairCode,
    decimal SourceAmount,
    decimal DestinationAmount,
    decimal CustomerRate,
    InstantQuoteStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? ConsumedAt);

public record InstantQuoteMaintenanceResultDto(
    int Candidates,
    int ExpiredQuotes,
    IReadOnlyList<InstantOperationFailureDto> Failures);

public record InstantOperationFailureDto(
    Guid EntityId,
    string Error);

public record InstantSettlementExceptionDto(
    Guid TradeId,
    string Reference,
    Guid InstantQuoteId,
    Guid InstantPairId,
    string PairCode,
    InstantTradeStatus Status,
    DateTime CreatedAt,
    DateTime? SettlementStartedAt,
    DateTime? FailedAt,
    string? FailureReason);
