using KorridorX.Data;
using KorridorX.Configuration;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

/// <summary>
/// Anonymous, indicative marketing previews. These are not executable or locked quotes;
/// only the authenticated transfer workflow creates a TransferQuote.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/public/marketing")]
public sealed class MarketingQuotesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarketingQuotesController(AppDbContext db) => _db = db;

    [HttpGet("quote-options")]
    public async Task<IActionResult> GetOptions(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ratePairs = (await _db.ExchangeRates.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.CustomerRate > 0 &&
                x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
            .Select(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode })
            .Distinct().ToListAsync(ct))
            .Select(x => (x.SourceCurrencyCode, x.DestinationCurrencyCode)).ToHashSet();

        var send = (await _db.CountryAssets.AsNoTracking()
            .Where(x => x.CanSend && x.Country.IsSupported && x.Country.IsSendCountry &&
                x.Asset.IsSupported)
            .Select(x => new { x.CountryCode, x.AssetCode }).ToListAsync(ct))
            .Select(x => (x.CountryCode, x.AssetCode)).ToHashSet();

        var receive = (await _db.CountryAssets.AsNoTracking()
            .Where(x => x.CanReceive && x.Country.IsSupported && x.Country.IsReceiveCountry &&
                x.Asset.IsSupported)
            .Select(x => new { x.CountryCode, x.AssetCode }).ToListAsync(ct))
            .Select(x => (x.CountryCode, x.AssetCode)).ToHashSet();

        var fees = await _db.TransferFees.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive &&
                x.TransferType == TransferType.ConsumerToConsumer &&
                x.MinAmount >= 0 && x.MinAmount <= 1_000_000_000m &&
                x.FixedFee >= 0 && x.PercentageFee >= 0 && (x.MaxAmount == null ||
                    (x.MaxAmount >= 0.01m && x.MaxAmount >= x.MinAmount)) &&
                x.FeeCurrencyCode == x.SourceCurrencyCode)
            .Select(x => new
            {
                x.SourceCountryCode, x.DestinationCountryCode,
                x.SourceCurrencyCode, x.DestinationCurrencyCode,
                x.MinAmount, x.MaxAmount
            }).ToListAsync(ct);

        var options = fees.Where(x =>
                x.SourceCurrencyCode != x.DestinationCurrencyCode &&
                ratePairs.Contains((x.SourceCurrencyCode, x.DestinationCurrencyCode)) &&
                send.Contains((x.SourceCountryCode, x.SourceCurrencyCode)) &&
                receive.Contains((x.DestinationCountryCode, x.DestinationCurrencyCode)))
            .GroupBy(x => new
            {
                x.SourceCountryCode, x.DestinationCountryCode,
                x.SourceCurrencyCode, x.DestinationCurrencyCode
            })
            .Select(group => new
            {
                group.Key.SourceCountryCode, group.Key.DestinationCountryCode,
                group.Key.SourceCurrencyCode, group.Key.DestinationCurrencyCode,
                MinimumSourceAmount = Math.Max(0.01m, group.Min(x => x.MinAmount)),
                MaximumSourceAmount = group.Any(x => x.MaxAmount == null)
                    ? 1_000_000_000m
                    : Math.Min(1_000_000_000m, group.Max(x => x.MaxAmount) ?? 1_000_000_000m)
            })
            .OrderBy(x => x.SourceCurrencyCode)
            .ThenBy(x => x.DestinationCurrencyCode)
            .ThenBy(x => x.SourceCountryCode)
            .ThenBy(x => x.DestinationCountryCode)
            .ToList();

        return Ok(ApiResponses.Ok(options, "Available indicative quote options."));
    }

    [HttpGet("quote-preview")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetPreview(
        [FromQuery] string? sourceCountryCode,
        [FromQuery] string? destinationCountryCode,
        [FromQuery] string? sourceCurrencyCode,
        [FromQuery] string? destinationCurrencyCode,
        [FromQuery] decimal sourceAmount,
        CancellationToken ct)
    {
        static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? "";
        var fromCountry = Normalize(sourceCountryCode);
        var toCountry = Normalize(destinationCountryCode);
        var fromAsset = Normalize(sourceCurrencyCode);
        var toAsset = Normalize(destinationCurrencyCode);
        if (!Valid(fromCountry, 10) || !Valid(toCountry, 10) ||
            !Valid(fromAsset, 20) || !Valid(toAsset, 20) || fromAsset == toAsset ||
            sourceAmount < 0.01m || sourceAmount > 1_000_000_000m)
            return BadRequest(ApiResponses.Fail("Enter a valid pair and amount.", "INVALID_QUOTE_REQUEST"));

        var sourceAvailable = await _db.CountryAssets.AsNoTracking().AnyAsync(x =>
            x.CountryCode == fromCountry && x.AssetCode == fromAsset && x.CanSend &&
            x.Country.IsSupported && x.Country.IsSendCountry && x.Asset.IsSupported, ct);
        var destinationAvailable = await _db.CountryAssets.AsNoTracking().AnyAsync(x =>
            x.CountryCode == toCountry && x.AssetCode == toAsset && x.CanReceive &&
            x.Country.IsSupported && x.Country.IsReceiveCountry && x.Asset.IsSupported, ct);

        if (!sourceAvailable || !destinationAvailable)
            return NotFound(ApiResponses.Fail("This corridor is unavailable.", "QUOTE_UNAVAILABLE"));

        var now = DateTime.UtcNow;
        var rate = await _db.ExchangeRates.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.CustomerRate > 0 &&
                x.SourceCurrencyCode == fromAsset && x.DestinationCurrencyCode == toAsset &&
                x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        var fee = await _db.TransferFees.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive &&
                x.TransferType == TransferType.ConsumerToConsumer &&
                x.SourceCountryCode == fromCountry && x.DestinationCountryCode == toCountry &&
                x.SourceCurrencyCode == fromAsset && x.DestinationCurrencyCode == toAsset &&
                x.FeeCurrencyCode == fromAsset && x.FixedFee >= 0 && x.PercentageFee >= 0 &&
                sourceAmount >= x.MinAmount && (x.MaxAmount == null || sourceAmount <= x.MaxAmount))
            .OrderByDescending(x => x.MinAmount)
            .FirstOrDefaultAsync(ct);
        if (rate == null || fee == null)
            return NotFound(ApiResponses.Fail("A quote is unavailable for this amount.", "QUOTE_UNAVAILABLE"));

        // Same arithmetic and fee selection as TransferQuoteService.CreateQuoteAsync.
        var feeAmount = fee.FixedFee + Math.Round(sourceAmount * fee.PercentageFee / 100m, 2);
        var destinationAmount = Math.Round(sourceAmount * rate.CustomerRate, 2);
        return Ok(ApiResponses.Ok(new
        {
            SourceCountryCode = fromCountry,
            DestinationCountryCode = toCountry,
            SourceCurrencyCode = fromAsset,
            DestinationCurrencyCode = toAsset,
            SourceAmount = sourceAmount,
            DestinationAmount = destinationAmount,
            CustomerRate = rate.CustomerRate,
            FeeAmount = feeAmount,
            FeeCurrencyCode = fee.FeeCurrencyCode,
            TotalPayableAmount = sourceAmount + feeAmount,
            AsOfUtc = rate.EffectiveFrom
        }, "Indicative quote only; confirm the final quote in the authenticated app."));
    }

    private static bool Valid(string code, int maxLength) =>
        code.Length is >= 2 && code.Length <= maxLength &&
        code.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
}
