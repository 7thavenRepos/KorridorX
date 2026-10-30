using System.Text.Json.Serialization;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessFunding;

// Strict request contract: no caller can provide an opening balance or another owner.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateBusinessWalletRequestDto(string AssetCode);

public sealed record AvailableBusinessWalletAssetDto(
    string AssetCode,
    string Name,
    string Symbol,
    int DecimalPlaces,
    bool IsDefault,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanSend,
    bool CanReceive,
    bool CanTrade,
    bool CanUseInstant,
    bool HasWallet,
    bool IsReserved);
