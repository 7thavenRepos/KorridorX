namespace KorridorX.Dtos.Wallets;

public sealed record ConsumerWalletDto(
    Guid Id,
    string AssetCode,
    string AssetName,
    string Symbol,
    int DecimalPlaces,
    string Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance,
    bool IsDefault,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanSend,
    bool CanReceive,
    bool CanTrade,
    bool CanUseInstant,
    DateTime CreatedAt);

public sealed record AvailableConsumerWalletAssetDto(
    string AssetCode,
    string AssetName,
    string Symbol,
    int DecimalPlaces,
    bool IsDefault,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanSend,
    bool CanReceive,
    bool CanTrade,
    bool CanUseInstant,
    bool IsAdded);

public sealed record CreateConsumerWalletRequestDto(string AssetCode);
