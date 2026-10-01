using System.Globalization;

namespace KorridorX.Dtos.Wallets;

// Informational available-balance estimate, not a transfer quote or a ledger balance.
public sealed record ConsumerWalletValuationDto(
    string BaseAssetCode,
    string BaseAssetName,
    string Symbol,
    int DecimalPlaces,
    string Status,
    decimal? EstimatedAvailableBalance,
    DateTime CalculatedAt,
    DateTime? ValidUntil,
    IReadOnlyList<ConsumerWalletValuationComponentDto> Components)
{
    // Preserve the server's decimal rounding when a client formats the estimate.
    public string? EstimatedAvailableBalanceText => EstimatedAvailableBalance?.ToString(
        $"F{DecimalPlaces}", CultureInfo.InvariantCulture);
}

public sealed record ConsumerWalletValuationComponentDto(
    Guid WalletId,
    string AssetCode,
    decimal AvailableBalance,
    decimal? EstimatedBaseAmount,
    string Status,
    Guid? ExchangeRateId = null,
    decimal? CustomerRate = null,
    DateTime? RateEffectiveFrom = null,
    DateTime? RateEffectiveTo = null);
