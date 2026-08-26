using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessDigitalAssets;

public sealed record BusinessDigitalAssetBalanceDto(
    Guid FinancialAccountId,
    string AssetCode,
    string AssetName,
    AssetType AssetType,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record BusinessDigitalAssetProviderOptionDto(
    string ProviderCode,
    string DisplayName,
    bool SupportsDepositAddress,
    bool SupportsDepositIntent,
    bool SupportsWithdrawal);

public sealed record BusinessDigitalAssetNetworkDto(
    Guid AssetNetworkId,
    string AssetCode,
    string AssetName,
    string NetworkCode,
    string NetworkName,
    int RequiredConfirmations,
    decimal MinimumDeposit,
    decimal MinimumWithdrawal,
    decimal WithdrawalFee,
    bool CanDeposit,
    bool CanWithdraw,
    IReadOnlyList<BusinessDigitalAssetProviderOptionDto> Providers);

public sealed record BusinessDigitalAssetWorkspaceDto(
    IReadOnlyList<BusinessDigitalAssetBalanceDto> Balances,
    IReadOnlyList<BusinessDigitalAssetNetworkDto> Networks,
    decimal TravelRuleThreshold);

public sealed record BusinessDigitalAssetActivityDto(
    Guid Id,
    string ActivityType,
    string Reference,
    string AssetCode,
    string? NetworkCode,
    decimal Amount,
    string Status,
    DateTime OccurredAt,
    DateTime? CompletedAt);