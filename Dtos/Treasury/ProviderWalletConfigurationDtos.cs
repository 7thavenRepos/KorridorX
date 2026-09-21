namespace KorridorX.Dtos.Treasury;

public sealed record RegisterProviderWalletRequest(string ProviderWalletId, string AssetCode, string? NetworkCode, string DisplayName, string Reason);
public sealed record ConfigureProviderWalletRequest(Guid Revision, string DisplayName, bool CollectionEnabled, bool PayoutEnabled,
    bool DefaultForCollection, bool DefaultForPayout, bool IsActive, string Reason);
public sealed record VerifyProviderWalletRequest(Guid Revision, string Reason);
public sealed record ImportProviderWalletsRequest(string Reason);
public sealed record DiscoveredProviderWallet(string ProviderWalletId, string AssetCode, bool IsCrypto, decimal Balance, bool IsActive);
public sealed record ProviderWalletConfigurationDto(Guid Id, string ProviderCode, string Environment, string ProviderWalletId,
    string AssetCode, string NetworkCode, string DisplayName, bool CollectionEnabled, bool PayoutEnabled,
    bool DefaultForCollection, bool DefaultForPayout, bool IsActive, bool VerifiedForCurrentConnection, DateTime? VerifiedAt,
    decimal? LastProviderBalance, Guid Revision);
