namespace KorridorX.Models.Enums;

public enum AssetType
{
    Fiat = 1,
    Crypto = 2
}

public enum AssetNetworkStatus
{
    Disabled = 0,
    Active = 1,
    Maintenance = 2
}

public enum DigitalAssetAddressStatus
{
    Active = 1,
    Disabled = 2
}

public enum DigitalAssetDestinationStatus
{
    Active = 1,
    Disabled = 2
}

public enum DigitalAssetTransactionDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum DigitalAssetTransactionStatus
{
    Observed = 1,
    Confirming = 2,
    Confirmed = 3,
    Failed = 4,
    Reorged = 5
}

public enum DigitalAssetWithdrawalStatus
{
    Pending = 1,
    Submitted = 2,
    Confirming = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

