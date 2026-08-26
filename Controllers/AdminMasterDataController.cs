using System.Text.RegularExpressions;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.MasterData;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/admin/master-data")]
[Authorize(Roles = "Admin,SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminMasterDataController : ControllerBase
{
    private static readonly Regex CountryCodePattern =
        new("^[A-Z]{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Iso3Pattern =
        new("^[A-Z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AssetCodePattern =
        new("^[A-Z0-9._-]{2,20}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;

    public AdminMasterDataController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var countries = await _db.Countries
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminCountryDto(
                x.Code,
                x.Name,
                x.Iso3Code,
                x.IsSupported,
                x.IsSendCountry,
                x.IsReceiveCountry,
                x.CreatedAt))
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            countries,
            "Country master data retrieved successfully."));
    }

    [HttpPost("countries")]
    public async Task<IActionResult> CreateCountry(
        [FromBody] CreateCountryRequestDto request,
        CancellationToken ct)
    {
        var code = NormalizeCountryCode(request.Code);
        var name = RequiredText(request.Name, 100, "Country name");
        var iso3 = NormalizeIso3(request.Iso3Code);

        if (await _db.Countries.AnyAsync(x => x.Code == code, ct))
            throw new InvalidOperationException($"Country '{code}' already exists.");

        var country = new Country
        {
            Code = code,
            Name = name,
            Iso3Code = iso3,
            IsSupported = request.IsSupported,
            IsSendCountry = request.IsSendCountry,
            IsReceiveCountry = request.IsReceiveCountry,
            CreatedAt = DateTime.UtcNow
        };

        _db.Countries.Add(country);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponses.Ok(
            ToDto(country),
            "Country created successfully."));
    }

    [HttpPut("countries/{countryCode}")]
    public async Task<IActionResult> UpdateCountry(
        string countryCode,
        [FromBody] UpdateCountryRequestDto request,
        CancellationToken ct)
    {
        var code = NormalizeCountryCode(countryCode);
        var country = await _db.Countries
            .SingleOrDefaultAsync(x => x.Code == code, ct)
            ?? throw new KeyNotFoundException($"Country '{code}' was not found.");

        country.Name = RequiredText(request.Name, 100, "Country name");
        country.Iso3Code = NormalizeIso3(request.Iso3Code);
        country.IsSupported = request.IsSupported;
        country.IsSendCountry = request.IsSendCountry;
        country.IsReceiveCountry = request.IsReceiveCountry;

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponses.Ok(
            ToDto(country),
            "Country updated successfully."));
    }

    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets(
        [FromQuery] AssetType? type,
        CancellationToken ct)
    {
        var query = _db.Assets.AsNoTracking();

        if (type.HasValue)
        {
            if (!Enum.IsDefined(type.Value))
                throw new ArgumentException("Unsupported asset type.");

            query = query.Where(x => x.Type == type.Value);
        }

        var assets = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Code)
            .Select(x => new AdminAssetDto(
                x.Code,
                x.Name,
                x.Symbol,
                x.Type,
                x.DecimalPlaces,
                x.IsStablecoin,
                x.IsSupported,
                x.DepositEnabled,
                x.WithdrawalEnabled,
                x.TradingEnabled,
                x.InstantEnabled,
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);

        return Ok(ApiResponses.Ok(
            assets,
            "Asset master data retrieved successfully."));
    }

    [HttpPost("assets")]
    public async Task<IActionResult> CreateAsset(
        [FromBody] CreateAssetRequestDto request,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Type))
            throw new ArgumentException("Unsupported asset type.");

        var code = NormalizeAssetCode(request.Code);
        var decimalPlaces = ValidateDecimalPlaces(request.DecimalPlaces);

        ValidateOperationalFlags(
            request.Type,
            request.IsSupported,
            request.DepositEnabled,
            request.WithdrawalEnabled,
            request.TradingEnabled,
            request.InstantEnabled,
            hasActiveDepositNetwork: false,
            hasActiveWithdrawalNetwork: false);

        if (await _db.Assets.AnyAsync(x => x.Code == code, ct))
            throw new InvalidOperationException($"Asset '{code}' already exists.");

        var asset = new Asset
        {
            Code = code,
            Name = RequiredText(request.Name, 100, "Asset name"),
            Symbol = RequiredText(request.Symbol, 20, "Asset symbol"),
            Type = request.Type,
            DecimalPlaces = decimalPlaces,
            IsStablecoin = request.Type == AssetType.Crypto && request.IsStablecoin,
            IsSupported = request.IsSupported,
            DepositEnabled = request.DepositEnabled,
            WithdrawalEnabled = request.WithdrawalEnabled,
            TradingEnabled = request.TradingEnabled,
            InstantEnabled = request.InstantEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponses.Ok(
            ToDto(asset),
            "Asset created successfully."));
    }

    [HttpPut("assets/{assetCode}")]
    public async Task<IActionResult> UpdateAsset(
        string assetCode,
        [FromBody] UpdateAssetRequestDto request,
        CancellationToken ct)
    {
        var code = NormalizeAssetCode(assetCode);
        var asset = await _db.Assets
            .Include(x => x.Networks)
            .SingleOrDefaultAsync(x => x.Code == code, ct)
            ?? throw new KeyNotFoundException($"Asset '{code}' was not found.");

        ValidateOperationalFlags(
            asset.Type,
            request.IsSupported,
            request.DepositEnabled,
            request.WithdrawalEnabled,
            request.TradingEnabled,
            request.InstantEnabled,
            asset.Networks.Any(x =>
                x.Status == AssetNetworkStatus.Active &&
                x.DepositEnabled),
            asset.Networks.Any(x =>
                x.Status == AssetNetworkStatus.Active &&
                x.WithdrawalEnabled));

        asset.Name = RequiredText(request.Name, 100, "Asset name");
        asset.Symbol = RequiredText(request.Symbol, 20, "Asset symbol");
        asset.DecimalPlaces = ValidateDecimalPlaces(request.DecimalPlaces);
        asset.IsStablecoin = asset.Type == AssetType.Crypto && request.IsStablecoin;
        asset.IsSupported = request.IsSupported;
        asset.DepositEnabled = request.DepositEnabled;
        asset.WithdrawalEnabled = request.WithdrawalEnabled;
        asset.TradingEnabled = request.TradingEnabled;
        asset.InstantEnabled = request.InstantEnabled;
        asset.LastUpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponses.Ok(
            ToDto(asset),
            "Asset updated successfully."));
    }

    private static AdminCountryDto ToDto(Country country) =>
        new(
            country.Code,
            country.Name,
            country.Iso3Code,
            country.IsSupported,
            country.IsSendCountry,
            country.IsReceiveCountry,
            country.CreatedAt);

    private static AdminAssetDto ToDto(Asset asset) =>
        new(
            asset.Code,
            asset.Name,
            asset.Symbol,
            asset.Type,
            asset.DecimalPlaces,
            asset.IsStablecoin,
            asset.IsSupported,
            asset.DepositEnabled,
            asset.WithdrawalEnabled,
            asset.TradingEnabled,
            asset.InstantEnabled,
            asset.CreatedAt,
            asset.LastUpdatedAt);

    private static string NormalizeCountryCode(string value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (!CountryCodePattern.IsMatch(code))
            throw new ArgumentException("Country code must be a two-letter ISO code.");

        return code;
    }

    private static string? NormalizeIso3(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var code = value.Trim().ToUpperInvariant();

        if (!Iso3Pattern.IsMatch(code))
            throw new ArgumentException("ISO-3 code must contain exactly three letters.");

        return code;
    }

    private static string NormalizeAssetCode(string value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (!AssetCodePattern.IsMatch(code))
            throw new ArgumentException(
                "Asset code must be 2-20 characters using letters, numbers, '.', '_' or '-'.");

        return code;
    }

    private static string RequiredText(string value, int maxLength, string fieldName)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length == 0 || text.Length > maxLength)
            throw new ArgumentException($"{fieldName} is required and cannot exceed {maxLength} characters.");

        return text;
    }

    private static void ValidateOperationalFlags(
        AssetType type,
        bool isSupported,
        bool depositEnabled,
        bool withdrawalEnabled,
        bool tradingEnabled,
        bool instantEnabled,
        bool hasActiveDepositNetwork,
        bool hasActiveWithdrawalNetwork)
    {
        if (!isSupported &&
            (depositEnabled ||
             withdrawalEnabled ||
             tradingEnabled ||
             instantEnabled))
        {
            throw new InvalidOperationException(
                "An unsupported asset cannot keep deposits, withdrawals, trading or instant trading enabled.");
        }

        if (type != AssetType.Crypto)
            return;

        if (depositEnabled && !hasActiveDepositNetwork)
        {
            throw new InvalidOperationException(
                "Configure at least one active deposit-enabled network before enabling crypto deposits.");
        }

        if (withdrawalEnabled && !hasActiveWithdrawalNetwork)
        {
            throw new InvalidOperationException(
                "Configure at least one active withdrawal-enabled network before enabling crypto withdrawals.");
        }
    }

    private static int ValidateDecimalPlaces(int decimalPlaces)
    {
        if (decimalPlaces is < 0 or > 18)
            throw new ArgumentException("Decimal places must be between 0 and 18.");

        return decimalPlaces;
    }
}
