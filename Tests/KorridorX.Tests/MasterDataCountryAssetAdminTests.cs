using System.Security.Claims;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.MasterData;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Services.Audit;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class MasterDataCountryAssetAdminTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public MasterDataCountryAssetAdminTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Administrator_upserts_mapping_and_keeps_one_default_per_country()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var setup = await CreateScenarioAsync(db);
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var controller = new AdminMasterDataController(db, audit);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        Guid.NewGuid().ToString())
                }, "Test"))
            }
        };

        await controller.UpsertCountryAsset(
            setup.CountryCode,
            setup.FirstAssetCode,
            EnabledRequest(isDefault: true),
            CancellationToken.None);

        await controller.UpsertCountryAsset(
            setup.CountryCode,
            setup.SecondAssetCode,
            EnabledRequest(isDefault: true),
            CancellationToken.None);

        var mappings = await db.CountryAssets
            .AsNoTracking()
            .Where(x => x.CountryCode == setup.CountryCode)
            .OrderBy(x => x.AssetCode)
            .ToListAsync();

        Assert.Equal(2, mappings.Count);
        Assert.False(mappings.Single(x =>
            x.AssetCode == setup.FirstAssetCode).IsDefault);
        Assert.True(mappings.Single(x =>
            x.AssetCode == setup.SecondAssetCode).IsDefault);
        Assert.All(mappings, mapping =>
        {
            Assert.True(mapping.CanSend);
            Assert.True(mapping.CanReceive);
            Assert.True(mapping.CanDeposit);
            Assert.True(mapping.CanWithdraw);
            Assert.True(mapping.CanTrade);
            Assert.True(mapping.CanUseInstant);
        });

        var auditActions = await db.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.EntityName == nameof(CountryAsset) &&
                mappings.Select(mapping => mapping.Id.ToString())
                    .Contains(x.EntityId!))
            .Select(x => x.Action)
            .ToListAsync();

        Assert.Contains("COUNTRY_ASSET_MAPPING_CREATED", auditActions);
    }

    [DatabaseIntegrationFact]
    public async Task Disabling_country_or_asset_cascades_to_existing_mapping()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var setup = await CreateScenarioAsync(db);
        var controller = new AdminMasterDataController(db);

        await controller.UpsertCountryAsset(
            setup.CountryCode,
            setup.FirstAssetCode,
            EnabledRequest(isDefault: true),
            CancellationToken.None);

        await controller.UpdateCountry(
            setup.CountryCode,
            new UpdateCountryRequestDto(
                "Disabled Corridor Country",
                null,
                false,
                false,
                false),
            CancellationToken.None);

        await controller.UpdateAsset(
            setup.FirstAssetCode,
            new UpdateAssetRequestDto(
                "Disabled Corridor Asset",
                setup.FirstAssetCode,
                2,
                false,
                false,
                false,
                false,
                false,
                false),
            CancellationToken.None);

        var mapping = await db.CountryAssets
            .AsNoTracking()
            .SingleAsync(x =>
                x.CountryCode == setup.CountryCode &&
                x.AssetCode == setup.FirstAssetCode);

        Assert.False(mapping.CanSend);
        Assert.False(mapping.CanReceive);
        Assert.False(mapping.CanDeposit);
        Assert.False(mapping.CanWithdraw);
        Assert.False(mapping.CanTrade);
        Assert.False(mapping.CanUseInstant);
        Assert.False(mapping.IsDefault);
    }

    [DatabaseIntegrationFact]
    public async Task Mapping_cannot_exceed_country_or_asset_capabilities()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var setup = await CreateScenarioAsync(db);
        var controller = new AdminMasterDataController(db);

        var country = await db.Countries.SingleAsync(x =>
            x.Code == setup.CountryCode);
        country.IsSendCountry = false;
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.UpsertCountryAsset(
                setup.CountryCode,
                setup.FirstAssetCode,
                EnabledRequest(isDefault: false),
                CancellationToken.None));

        Assert.Contains(
            "not a send country",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static UpsertCountryAssetRequestDto EnabledRequest(bool isDefault) =>
        new(
            true,
            true,
            true,
            true,
            true,
            true,
            isDefault,
            "Configure the test corridor mapping.");

    private static async Task<Scenario> CreateScenarioAsync(AppDbContext db)
    {
        var countryCode = await NextCountryCodeAsync(db);
        var firstAssetCode = $"F{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var secondAssetCode = $"G{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        db.Countries.Add(new Country
        {
            Code = countryCode,
            Name = $"Corridor Country {countryCode}",
            IsSupported = true,
            IsSendCountry = true,
            IsReceiveCountry = true
        });

        foreach (var code in new[] { firstAssetCode, secondAssetCode })
        {
            db.Assets.Add(new Asset
            {
                Code = code,
                Name = $"Corridor Asset {code}",
                Symbol = code,
                Type = AssetType.Fiat,
                DecimalPlaces = 2,
                IsSupported = true,
                DepositEnabled = true,
                WithdrawalEnabled = true,
                TradingEnabled = true,
                InstantEnabled = true
            });
        }

        await db.SaveChangesAsync();
        return new Scenario(countryCode, firstAssetCode, secondAssetCode);
    }

    private static async Task<string> NextCountryCodeAsync(AppDbContext db)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var code = string.Concat(
                (char)('A' + bytes[0] % 26),
                (char)('A' + bytes[1] % 26));

            if (!await db.Countries.AnyAsync(x => x.Code == code))
                return code;
        }

        throw new InvalidOperationException("Unable to allocate a test country code.");
    }

    private sealed record Scenario(
        string CountryCode,
        string FirstAssetCode,
        string SecondAssetCode);
}
