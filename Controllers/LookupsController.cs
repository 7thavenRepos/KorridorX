using KorridorX.Data;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
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
    public async Task<IActionResult> GetAssets(CancellationToken ct)
    {
        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.IsSupported)
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
        CancellationToken ct)
    {
        var normalizedAssetCode = string.IsNullOrWhiteSpace(assetCode)
            ? null
            : assetCode.Trim().ToUpperInvariant();

        var query = _db.AssetNetworks
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x =>
                x.Asset.Type == AssetType.Crypto &&
                x.Asset.IsSupported);

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
    public async Task<IActionResult> GetCorridors(CancellationToken ct)
    {
        var corridors = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Include(x => x.Asset)
            .Where(x =>
                x.Country.IsSupported &&
                x.Asset.IsSupported &&
                (x.CanSend ||
                 x.CanReceive ||
                 x.CanDeposit ||
                 x.CanWithdraw ||
                 x.CanTrade ||
                 x.CanUseInstant))
            .OrderBy(x => x.Country.Name)
            .ThenBy(x => x.Asset.Type)
            .ThenBy(x => x.Asset.Code)
            .Select(x => new
            {
                CountryCode = x.Country.Code,
                CountryName = x.Country.Name,
                AssetCode = x.Asset.Code,
                AssetName = x.Asset.Name,
                x.Asset.Type,
                x.CanSend,
                x.CanReceive,
                x.CanDeposit,
                x.CanWithdraw,
                x.CanTrade,
                x.CanUseInstant,
                x.IsDefault
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(corridors, "Corridors retrieved successfully."));
    }
}
