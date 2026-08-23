using KorridorX.Models.Enums;

namespace KorridorX.Models.Lookups;

public class AssetNetwork
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AssetCode { get; set; } = "";
    public Asset Asset { get; set; } = null!;

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
    public AssetNetworkStatus Status { get; set; } = AssetNetworkStatus.Disabled;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }
}
