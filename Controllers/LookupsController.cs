using KorridorX.Data;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LookupsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("business-types")]
    public async Task<IActionResult> GetBusinessTypes(CancellationToken ct) => Ok(ApiResponses.Ok(
        await _db.BusinessTypes.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Code, x.Name }).ToListAsync(ct)));

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var countries = await _db.Countries
            .AsNoTracking()
            .Where(x => x.IsSupported)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Code,
                x.Iso3Code,
                x.Name,
                x.IsSendCountry,
                x.IsReceiveCountry
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(countries, "Countries retrieved successfully."));
    }

    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets([FromQuery] string? channel, CancellationToken ct)
    {
        var lookupChannel = AssetChannelAccessPolicy.ResolveChannel(channel, User);
        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.IsSupported && (lookupChannel == AssetLookupChannel.AdminAll || (lookupChannel == AssetLookupChannel.Business ? x.BusinessEnabled : x.IndividualEnabled)))
            .OrderBy(x => x.Code)
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.Symbol,
                x.DecimalPlaces,
                x.Type,
                x.IsStablecoin,
                x.DepositEnabled,
                x.WithdrawalEnabled,
                x.TradingEnabled,
                x.InstantEnabled
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(assets, "Assets retrieved successfully."));
    }

    [HttpGet("asset-networks")]
    public async Task<IActionResult> GetAssetNetworks(
        [FromQuery] string? assetCode,
        [FromQuery] string? channel,
        CancellationToken ct)
    {
        var lookupChannel = AssetChannelAccessPolicy.ResolveChannel(channel, User);
        var normalizedAssetCode = string.IsNullOrWhiteSpace(assetCode)
            ? null
            : assetCode.Trim().ToUpperInvariant();

        var query = _db.AssetNetworks
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x =>
                x.Asset.Type == AssetType.Crypto &&
                x.Asset.IsSupported &&
                (lookupChannel == AssetLookupChannel.AdminAll || (lookupChannel == AssetLookupChannel.Business ? x.Asset.BusinessEnabled : x.Asset.IndividualEnabled)));

        if (normalizedAssetCode is not null)
        {
            query = query.Where(x => x.AssetCode == normalizedAssetCode);
        }

        var networks = await query
            .OrderBy(x => x.AssetCode)
            .ThenBy(x => x.NetworkCode)
            .Select(x => new
            {
                x.Id,
                x.AssetCode,
                x.NetworkCode,
                x.Name,
                x.Status,
                x.DepositEnabled,
                x.WithdrawalEnabled
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            networks,
            "Asset networks retrieved successfully."));
    }

    [HttpGet("corridors")]
    public async Task<IActionResult> GetCorridors([FromQuery] string? channel, CancellationToken ct)
    {
        var lookupChannel = AssetChannelAccessPolicy.ResolveChannel(channel, User);
        // Consumer selector compatibility: every available channel asset is visible
        // in both dropdowns for each supported country. REAL corridor execution
        // remains gated by CountryAssets in quote and transfer services.
        var countries = await _db.Countries.AsNoTracking()
            .Where(x => x.IsSupported)
            .Select(x => new LookupCountry(x.Code, x.Name))
            .ToListAsync(ct);
        var assets = await _db.Assets.AsNoTracking()
            .Where(x => x.IsSupported && (lookupChannel == AssetLookupChannel.AdminAll || (lookupChannel == AssetLookupChannel.Business ? x.BusinessEnabled : x.IndividualEnabled)))
            .Select(x => new LookupAsset(x.Code, x.Name, x.Type))
            .ToListAsync(ct);
        var codes = assets.Select(x => x.Code).ToArray();
        var countryCodes = countries.Select(x => x.Code).ToArray();
        var mappings = await _db.CountryAssets.AsNoTracking()
            .Where(x => countryCodes.Contains(x.CountryCode) && codes.Contains(x.AssetCode))
            .Select(x => new LookupMapping(
                x.CountryCode, x.AssetCode, x.CanSend, x.CanReceive,
                x.CanDeposit, x.CanWithdraw, x.CanTrade, x.CanUseInstant, x.IsDefault))
            .ToListAsync(ct);

        var options = AssetSelectionCatalog.Build(countries, assets, mappings);
        return Ok(ApiResponses.Ok(options,
            "Channel asset selections retrieved. Transfer availability is validated when quoting."));
    }

}
