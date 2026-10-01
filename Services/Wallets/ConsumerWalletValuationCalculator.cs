using KorridorX.Dtos.Wallets;
using KorridorX.Models.Fx;

namespace KorridorX.Services.Wallets;

public static class ConsumerWalletValuationCalculator
{
    public static ConsumerWalletValuationDto Calculate(
        IReadOnlyList<ConsumerWalletDto> wallets,
        IReadOnlyList<AvailableConsumerWalletAssetDto> availableAssets,
        IReadOnlyList<ExchangeRate> rates,
        string baseAssetCode,
        int rateStaleMinutes,
        DateTime now)
    {
        var code = baseAssetCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code) || code.Length > 20)
            throw new InvalidOperationException("A configured wallet currency is required.");
        if (rateStaleMinutes < 1)
            throw new InvalidOperationException("FX rate freshness must be configured.");
        var baseAsset = availableAssets.FirstOrDefault(x => x.AssetCode == code && x.IsAdded);
        if (baseAsset is null || !wallets.Any(x => x.AssetCode == code))
            throw new InvalidOperationException("Choose one of your supported fiat wallets as the display currency.");
        if (baseAsset.DecimalPlaces is < 0 or > 28)
            throw new InvalidOperationException("The display currency precision is invalid.");

        var eligible = availableAssets.Select(x => x.AssetCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var components = new List<ConsumerWalletValuationComponentDto>();
        DateTime? validUntil = null;
        foreach (var wallet in wallets)
        {
            var amount = wallet.AvailableBalance;
            if (amount == 0m)
            {
                components.Add(new(wallet.Id, wallet.AssetCode, amount, 0m, "ZeroBalance"));
                continue;
            }
            if (!eligible.Contains(wallet.AssetCode))
            {
                components.Add(new(wallet.Id, wallet.AssetCode, amount, null, "UnsupportedAsset"));
                continue;
            }
            if (!string.Equals(wallet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                components.Add(new(wallet.Id, wallet.AssetCode, amount, null, "InactiveWallet"));
                continue;
            }
            if (wallet.AssetCode == code)
            {
                components.Add(new(wallet.Id, wallet.AssetCode, amount,
                    Math.Round(amount, baseAsset.DecimalPlaces, MidpointRounding.ToEven), "Native"));
                continue;
            }

            // A directional customer rate is authoritative. Do not invert or triangulate pairs.
            var rate = rates.Where(x =>
                    x.SourceCurrencyCode == wallet.AssetCode && x.DestinationCurrencyCode == code &&
                    x.IsActive && !x.IsDeleted && x.EffectiveFrom <= now &&
                    (x.EffectiveTo is null || x.EffectiveTo > now))
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            if (rate is null)
            {
                components.Add(new(wallet.Id, wallet.AssetCode, amount, null, "MissingRate"));
                continue;
            }
            var expiresAt = rate.EffectiveFrom.AddMinutes(rateStaleMinutes);
            if (rate.EffectiveTo is { } until && until < expiresAt) expiresAt = until;
            var status = expiresAt <= now ? "StaleRate" : rate.CustomerRate <= 0m ? "InvalidRate" : "Converted";
            decimal? converted = null;
            if (status == "Converted")
            {
                try
                {
                    converted = Math.Round(amount * rate.CustomerRate, baseAsset.DecimalPlaces,
                        MidpointRounding.ToEven);
                    validUntil = validUntil is null || expiresAt < validUntil ? expiresAt : validUntil;
                }
                catch (OverflowException) { status = "AmountOverflow"; }
            }
            components.Add(new(wallet.Id, wallet.AssetCode, amount, converted, status,
                rate.Id, rate.CustomerRate, rate.EffectiveFrom, rate.EffectiveTo));
        }

        decimal? total = null;
        if (components.All(x => x.EstimatedBaseAmount.HasValue))
        {
            try { total = components.Sum(x => x.EstimatedBaseAmount!.Value); }
            catch (OverflowException) { /* An unrepresentable total is unavailable, never partial. */ }
        }
        return new(code, baseAsset.AssetName, baseAsset.Symbol, baseAsset.DecimalPlaces,
            total.HasValue ? "Complete" : "Unavailable", total, now,
            total.HasValue ? validUntil : null, components);
    }
}
