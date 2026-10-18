using KorridorX.Dtos.Payments;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;

namespace KorridorX.Dtos.PaymentChannels;

public sealed record PaymentChannelCapabilityDto(string Code, string ProviderCode,
    PaymentChannelDirection Direction, string AssetCode, PaymentMethod PaymentMethod,
    string Flow, string DisplayName, bool IndividualSupported, bool BusinessSupported,
    bool Implemented, string? BlockedReason, string Note);

public sealed record PaymentChannelDto(Guid Id, Guid Revision, string Environment,
    string ProviderCode, Guid ProviderWalletConfigurationId, string WalletDisplayName,
    string WalletAssetCode, string AssetCode, string CountryCode, PaymentChannelDirection Direction,
    string CapabilityCode, string DisplayName, string Description,
    bool IndividualEnabled, bool BusinessEnabled, bool ProviderCapabilityConfirmed,
    PaymentChannelStatus Status, int SortOrder, decimal MinimumAmount, decimal? MaximumAmount,
    bool Ready, string[] BlockedReasons, string[] Warnings);

public sealed record SavePaymentChannelRequest(Guid? Revision, Guid ProviderWalletConfigurationId,
    string AssetCode, string CountryCode, PaymentChannelDirection Direction, string CapabilityCode,
    string DisplayName, string? Description, bool IndividualEnabled, bool BusinessEnabled,
    bool ProviderCapabilityConfirmed, PaymentChannelStatus Status, int SortOrder,
    decimal MinimumAmount, decimal? MaximumAmount, string Reason);

public sealed record SetPaymentChannelStatusRequest(Guid Revision, PaymentChannelStatus Status, string Reason);
public sealed record CreateWalletDepositAccountRequest(Guid ChannelId);
public sealed record WalletDepositAccountDto(Guid Id, Guid WalletId, Guid ChannelId,
    string AssetCode, string Status, bool CanRetry, string? FailureReason, FundingInstructionsDto Instructions);
public sealed record WithdrawalChannelOptionDto(Guid Id, string DisplayName, string DestinationAssetCode,
    string DestinationCountryCode, PaymentMethod PaymentMethod, string Description);
