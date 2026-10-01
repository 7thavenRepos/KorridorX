using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletValuationService : IConsumerWalletValuationService
{
    private readonly AppDbContext _db;
    private readonly IConsumerWalletService _wallets;
    private readonly TreasuryOptions _options;

    public ConsumerWalletValuationService(
        AppDbContext db,
        IConsumerWalletService wallets,
        IOptions<TreasuryOptions> options)
    {
        _db = db;
        _wallets = wallets;
        _options = options.Value;
    }

    public async Task<ConsumerWalletValuationDto> GetValuationAsync(
        Guid userId, string baseAssetCode, CancellationToken ct = default)
    {
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
        var rates = await _db.ExchangeRates.AsNoTracking()
            .Where(x => x.DestinationCurrencyCode == code && sourceCodes.Contains(x.SourceCurrencyCode) &&
                x.IsActive && !x.IsDeleted)
            .ToListAsync(ct);

        return ConsumerWalletValuationCalculator.Calculate(
            wallets, assets, rates, code, _options.FxRateStaleMinutes, DateTime.UtcNow);
    }
}
