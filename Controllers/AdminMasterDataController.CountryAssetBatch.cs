using KorridorX.Dtos.Audit;
using KorridorX.Dtos.MasterData;
using KorridorX.Infrastructure;
using KorridorX.Models.Lookups;
using KorridorX.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

public sealed partial class AdminMasterDataController
{
    [HttpPost("country-assets/{countryCode}/batch")]
    public async Task<IActionResult> AddCountryAssets(
        string countryCode, [FromBody] BatchCountryAssetRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedCountry = NormalizeCountryCode(countryCode);
        var assetCodes = CountryAssetBatchRules.NormalizeAssetCodes(request.AssetCodes);
        var reason = RequiredText(request.Reason, 1000, "Change reason");
        var capabilities = CountryAssetBatchRules.Capabilities(request, reason);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var country = await _db.Countries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == normalizedCountry, ct)
            ?? throw new KeyNotFoundException($"Country '{normalizedCountry}' was not found.");
        var assets = await _db.Assets.AsNoTracking().Where(x => assetCodes.Contains(x.Code)).ToListAsync(ct);
        var missing = assetCodes.Except(assets.Select(x => x.Code), StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            throw new KeyNotFoundException($"Configured assets were not found: {string.Join(", ", missing)}.");
        var existing = (await _db.CountryAssets.AsNoTracking()
            .Where(x => x.CountryCode == normalizedCountry && assetCodes.Contains(x.AssetCode))
            .Select(x => x.AssetCode).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        // Validate every NEW mapping before writing any. Already mapped assets are never re-enabled or edited.
        foreach (var asset in assets.Where(x => !existing.Contains(x.Code)))
        {
            try { ValidateCountryAssetRequest(country, asset, capabilities); }
            catch (InvalidOperationException ex) { throw new InvalidOperationException($"{asset.Code}: {ex.Message}", ex); }
        }

        var created = 0;
        // Canonical order avoids reverse lock acquisition between overlapping bulk requests.
        foreach (var assetCode in assetCodes.Where(x => !existing.Contains(x)))
        {
            var id = Guid.NewGuid();
            // The existing unique country/asset index also protects simultaneous calls and legacy writers.
            // All values are parameters; conflicting rows are NOT updated, including disabled/default rows.
            var inserted = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CountryAssets"
                    ("Id", "CountryCode", "AssetCode", "CanSend", "CanReceive", "CanDeposit",
                     "CanWithdraw", "CanTrade", "CanUseInstant", "IsDefault")
                VALUES ({id}, {normalizedCountry}, {assetCode}, {request.CanSend}, {request.CanReceive},
                        {request.CanDeposit}, {request.CanWithdraw}, {request.CanTrade}, {request.CanUseInstant}, FALSE)
                ON CONFLICT ("CountryCode", "AssetCode") DO NOTHING
                """, ct);
            if (inserted == 0) continue;
            created++;
            _audit?.Stage(new AuditRecordRequest(
                Action: "COUNTRY_ASSET_MAPPING_CREATED", Category: "MasterData",
                EntityName: nameof(CountryAsset), EntityId: id.ToString(),
                NewValues: new { CountryCode = normalizedCountry, AssetCode = assetCode,
                    request.CanSend, request.CanReceive, request.CanDeposit, request.CanWithdraw,
                    request.CanTrade, request.CanUseInstant, IsDefault = false },
                Metadata: new { Reason = reason, Source = "CountryAssetBatch", SelectedAssetCount = assetCodes.Length },
                UserId: GetUserId()));
        }
        await _db.SaveChangesAsync(ct); // Audit entries commit atomically with all inserted mappings.
        var mappings = await _db.CountryAssets.AsNoTracking()
            .Where(x => x.CountryCode == normalizedCountry && assetCodes.Contains(x.AssetCode))
            .OrderBy(x => x.AssetCode).ToListAsync(ct);
        var byCode = assets.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var response = new BatchCountryAssetResultDto(normalizedCountry, created, assetCodes.Length - created,
            mappings.Select(x => ToDto(x, country, byCode[x.AssetCode])).ToArray());
        await transaction.CommitAsync(ct);
        return Ok(ApiResponses.Ok(response,
            $"{created} mapping(s) added; {assetCodes.Length - created} already mapped and left unchanged."));
    }
}
