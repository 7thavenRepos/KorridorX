using KorridorX.Configuration;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Fx;
using KorridorX.Services.Fx;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletValuationService : IConsumerWalletValuationService
{
    private readonly ILiveFxRateService _liveRates;
    private readonly IConsumerWalletService _wallets;
    private readonly TreasuryOptions _options;

    public ConsumerWalletValuationService(
        ILiveFxRateService liveRates,
        IConsumerWalletService wallets,
        IOptions<TreasuryOptions> options)
    {
        _liveRates = liveRates;
        _wallets = wallets;
        _options = options.Value;
    }

    public async Task<ConsumerWalletValuationDto> GetValuationAsync(
        Guid userId, string baseAssetCode, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Both lists use the existing Consumer owner/profile/master-data boundaries.
        var wallets = await _wallets.GetWalletsAsync(userId, ct);
        var assets = await _wallets.GetAvailableAssetsAsync(userId, ct);
        var code = baseAssetCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code) || code.Length > 20 ||
            !wallets.Any(x => x.AssetCode == code) ||
            !assets.Any(x => x.AssetCode == code && x.IsAdded))
            throw new InvalidOperationException("Choose one of your supported fiat wallets as the display currency.");

        var sourceCodes = wallets.Where(x => x.AvailableBalance != 0m && x.AssetCode != code)
            .Select(x => x.AssetCode).Distinct().ToList();
        var rates = new List<ExchangeRate>();
        foreach (var source in sourceCodes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Each displayed conversion has its own fresh provider observation. Never read
                // an older stored snapshot when the current provider/configuration is unavailable.
                var rate = await _liveRates.UseFreshRateAsync(source, code, FxRateUse.Valuation,
                    (current, _, _) => Task.FromResult(current), ct);
                rates.Add(rate);
            }
            catch (InvalidOperationException)
            {
                ct.ThrowIfCancellationRequested();
                // Existing calculator retains unpriced components and withholds the combined total.
            }
        }

        ct.ThrowIfCancellationRequested();
        return ConsumerWalletValuationCalculator.Calculate(
            wallets, assets, rates, code, _options.FxRateStaleMinutes, DateTime.UtcNow);
    }
}
