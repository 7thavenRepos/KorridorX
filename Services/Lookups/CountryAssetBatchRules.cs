using System.Text.RegularExpressions;
using KorridorX.Dtos.MasterData;
using KorridorX.Models.Lookups;

namespace KorridorX.Services.Lookups;

public static class CountryAssetBatchRules
{
    public const int MaximumAssets = 100;
    public static string[] NormalizeAssetCodes(IReadOnlyList<string>? codes)
    {
        if (codes is null || codes.Count == 0 || codes.Count > MaximumAssets)
            throw new ArgumentException($"Select between 1 and {MaximumAssets} configured assets.");
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var value = code?.Trim().ToUpperInvariant() ?? "";
            if (!Regex.IsMatch(value, "^[A-Z0-9._-]{2,20}$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Every selection must be a valid configured asset code.");
            normalized.Add(value);
        }
        return normalized.ToArray();
    }

    public static UpsertCountryAssetRequestDto Capabilities(BatchCountryAssetRequestDto request, string reason) =>
        new(request.CanSend, request.CanReceive, request.CanDeposit, request.CanWithdraw,
            request.CanTrade, request.CanUseInstant, false, reason);

    public static void ValidateCountryAssetRequest(
        Country country,
        Asset asset,
        UpsertCountryAssetRequestDto request)
    {
        var anyCapability = request.CanSend ||
            request.CanReceive ||
            request.CanDeposit ||
            request.CanWithdraw ||
            request.CanTrade ||
            request.CanUseInstant;

        if (anyCapability && !country.IsSupported)
            throw new InvalidOperationException(
                "Capabilities cannot be enabled for an unsupported country.");

        if (anyCapability && !asset.IsSupported)
            throw new InvalidOperationException(
                "Capabilities cannot be enabled for an unsupported asset.");

        if (request.CanSend && !country.IsSendCountry)
            throw new InvalidOperationException(
                "Sending cannot be enabled because the country is not a send country.");

        if (request.CanReceive && !country.IsReceiveCountry)
            throw new InvalidOperationException(
                "Receiving cannot be enabled because the country is not a receive country.");

        if (request.CanDeposit && !asset.DepositEnabled)
            throw new InvalidOperationException(
                "Deposits cannot be enabled because the asset has deposits disabled.");

        if (request.CanWithdraw && !asset.WithdrawalEnabled)
            throw new InvalidOperationException(
                "Withdrawals cannot be enabled because the asset has withdrawals disabled.");

        if (request.CanTrade && !asset.TradingEnabled)
            throw new InvalidOperationException(
                "Trading cannot be enabled because the asset has trading disabled.");

        if (request.CanUseInstant && !asset.InstantEnabled)
            throw new InvalidOperationException(
                "Instant use cannot be enabled because the asset has instant operations disabled.");

        if (request.IsDefault && (!country.IsSupported || !asset.IsSupported))
            throw new InvalidOperationException(
                "The default mapping must use a supported country and asset.");

        if (request.IsDefault && !anyCapability)
            throw new InvalidOperationException(
                "The default mapping must have at least one active capability.");
    }

}
