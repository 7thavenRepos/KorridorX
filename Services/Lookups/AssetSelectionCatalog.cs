using KorridorX.Models.Enums;

namespace KorridorX.Services.Lookups;

public sealed record LookupCountry(string Code, string Name);
public sealed record LookupAsset(string Code, string Name, AssetType Type);
public sealed record LookupMapping(
    string CountryCode,
    string AssetCode,
    bool CanSend,
    bool CanReceive,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanTrade,
    bool CanUseInstant,
    bool IsDefault);

public sealed record AssetSelectionCorridor(
    string CountryCode,
    string CountryName,
    string AssetCode,
    string AssetName,
    AssetType Type,
    bool CanSend,
    bool CanReceive,
    bool CanDeposit,
    bool CanWithdraw,
    bool CanTrade,
    bool CanUseInstant,
    bool IsDefault,
    bool ActualCanSend,
    bool ActualCanReceive);

public static class AssetSelectionCatalog
{
    // Legacy Flutter filters by CountryCode and CanSend/CanReceive. These flags
    // indicate selector VISIBILITY only. ActualCanSend/Receive describe operational
    // corridor support; quote/transfer services must recheck real CountryAssets.
    public static IReadOnlyList<AssetSelectionCorridor> Build(
        IReadOnlyList<LookupCountry> countries,
        IReadOnlyList<LookupAsset> channelAssets,
        IReadOnlyList<LookupMapping> existingMappings)
    {
        var mappings = existingMappings.ToDictionary(
            x => (x.CountryCode.ToUpperInvariant(), x.AssetCode.ToUpperInvariant()));
        var result = new List<AssetSelectionCorridor>(countries.Count * channelAssets.Count);
        foreach (var country in countries.OrderBy(x => x.Name))
        foreach (var asset in channelAssets.OrderBy(x => x.Type).ThenBy(x => x.Code))
        {
            mappings.TryGetValue(
                (country.Code.ToUpperInvariant(), asset.Code.ToUpperInvariant()), out var mapping);
            result.Add(new AssetSelectionCorridor(
                country.Code, country.Name, asset.Code, asset.Name, asset.Type,
                CanSend: true, CanReceive: true,
                CanDeposit: mapping?.CanDeposit ?? false,
                CanWithdraw: mapping?.CanWithdraw ?? false,
                CanTrade: mapping?.CanTrade ?? false,
                CanUseInstant: mapping?.CanUseInstant ?? false,
                IsDefault: mapping?.IsDefault ?? false,
                ActualCanSend: mapping?.CanSend ?? false,
                ActualCanReceive: mapping?.CanReceive ?? false));
        }
        return result;
    }
}
