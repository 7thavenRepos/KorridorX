using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.DigitalAssets;

public sealed class DigitalAssetAdminAssetDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public AssetType Type { get; set; }
    public int DecimalPlaces { get; set; }
    public bool IsStablecoin { get; set; }
    public bool IsSupported { get; set; }
    public bool DepositEnabled { get; set; }
    public bool WithdrawalEnabled { get; set; }
    public bool TradingEnabled { get; set; }
    public bool InstantEnabled { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public IReadOnlyList<DigitalAssetAdminNetworkDto> Networks { get; set; } =
        Array.Empty<DigitalAssetAdminNetworkDto>();
    public IReadOnlyList<DigitalAssetCountryAvailabilityDto> CountryAvailability { get; set; } =
        Array.Empty<DigitalAssetCountryAvailabilityDto>();
}

public sealed class DigitalAssetAdminNetworkDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = "";
    public string NetworkCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NativeAssetCode { get; set; }
    public string? ContractAddress { get; set; }
    public int RequiredConfirmations { get; set; }
    public decimal MinimumDeposit { get; set; }
    public decimal MinimumWithdrawal { get; set; }
    public decimal WithdrawalFee { get; set; }
    public bool DepositEnabled { get; set; }
    public bool WithdrawalEnabled { get; set; }
    public AssetNetworkStatus Status { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public sealed class DigitalAssetCountryAvailabilityDto
{
    public string CountryCode { get; set; } = "";
    public string CountryName { get; set; } = "";
    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
    public bool CanDeposit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanTrade { get; set; }
    public bool CanUseInstant { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateDigitalAssetEnablementRequestDto
{
    public bool IsSupported { get; set; }
    public bool DepositEnabled { get; set; }
    public bool WithdrawalEnabled { get; set; }
    public bool TradingEnabled { get; set; }
    public bool InstantEnabled { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class UpdateDigitalAssetNetworkRequestDto
{
    public AssetNetworkStatus Status { get; set; }

    [Range(0, 1_000_000)]
    public int RequiredConfirmations { get; set; }

    [Range(typeof(decimal), "0", "999999999999999999")]
    public decimal MinimumDeposit { get; set; }

    [Range(typeof(decimal), "0", "999999999999999999")]
    public decimal MinimumWithdrawal { get; set; }

    [Range(typeof(decimal), "0", "999999999999999999")]
    public decimal WithdrawalFee { get; set; }

    public bool DepositEnabled { get; set; }
    public bool WithdrawalEnabled { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class UpdateDigitalAssetCountryAvailabilityRequestDto
{
    public bool CanDeposit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanTrade { get; set; }
    public bool CanUseInstant { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}
