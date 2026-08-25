using KorridorX.Models.Enums;

namespace KorridorX.Dtos.MasterData;

public sealed record AdminCountryDto(
    string Code,
    string Name,
    string? Iso3Code,
    bool IsSupported,
    bool IsSendCountry,
    bool IsReceiveCountry,
    DateTime CreatedAt);

public sealed record CreateCountryRequestDto(
    string Code,
    string Name,
    string? Iso3Code,
    bool IsSupported,
    bool IsSendCountry,
    bool IsReceiveCountry);

public sealed record UpdateCountryRequestDto(
    string Name,
    string? Iso3Code,
    bool IsSupported,
    bool IsSendCountry,
    bool IsReceiveCountry);

public sealed record AdminAssetDto(
    string Code,
    string Name,
    string Symbol,
    AssetType Type,
    int DecimalPlaces,
    bool IsStablecoin,
    bool IsSupported,
    bool DepositEnabled,
    bool WithdrawalEnabled,
    bool TradingEnabled,
    bool InstantEnabled,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record CreateAssetRequestDto(
    string Code,
    string Name,
    string Symbol,
    AssetType Type,
    int DecimalPlaces,
    bool IsStablecoin,
    bool IsSupported,
    bool DepositEnabled,
    bool WithdrawalEnabled,
    bool TradingEnabled,
    bool InstantEnabled);

public sealed record UpdateAssetRequestDto(
    string Name,
    string Symbol,
    int DecimalPlaces,
    bool IsStablecoin,
    bool IsSupported,
    bool DepositEnabled,
    bool WithdrawalEnabled,
    bool TradingEnabled,
    bool InstantEnabled);
