using System.Text.Json.Serialization;

namespace KorridorX.Dtos.MasterData;

// Creation only. Default changes and existing-mapping edits remain on the reviewed PUT route.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BatchCountryAssetRequestDto(
    string[] AssetCodes,
    bool CanSend,
    bool CanReceive,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanTrade,
    bool CanUseInstant,
    string Reason);

public sealed record BatchCountryAssetResultDto(
    string CountryCode,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<AdminCountryAssetDto> Mappings);
