using KorridorX.Data;
using KorridorX.Infrastructure;
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

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct)
    {
        var currencies = await _db.Currencies
            .AsNoTracking()
            .Where(x => x.IsSupported)
            .OrderBy(x => x.Code)
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.Symbol,
                x.DecimalPlaces,
                x.IsFiat,
                x.IsStablecoin
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(currencies, "Currencies retrieved successfully."));
    }

    [HttpGet("corridors")]
    public async Task<IActionResult> GetCorridors(CancellationToken ct)
    {
        var corridors = await _db.CountryCurrencies
            .AsNoTracking()
            .Include(x => x.Country)
            .Include(x => x.Currency)
            .OrderBy(x => x.Country.Name)
            .Select(x => new
            {
                CountryCode = x.Country.Code,
                CountryName = x.Country.Name,
                CurrencyCode = x.Currency.Code,
                CurrencyName = x.Currency.Name,
                x.CanSend,
                x.CanReceive,
                x.IsDefault
            })
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(corridors, "Corridors retrieved successfully."));
    }
}