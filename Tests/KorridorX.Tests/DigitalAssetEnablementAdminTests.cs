using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Dtos.MasterData;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Services.DigitalAssets;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class DigitalAssetEnablementAdminTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public DigitalAssetEnablementAdminTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Administrator_can_create_network_then_enable_new_crypto_asset()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var assetCode = $"M{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var controller = new AdminMasterDataController(db);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateAsset(
                new CreateAssetRequestDto(
                    assetCode,
                    $"Master Crypto {assetCode}",
                    assetCode,
                    AssetType.Crypto,
                    8,
                    false,
                    true,
                    true,
                    true,
                    true,
                    false),
                CancellationToken.None));

        Assert.Contains(
            "active deposit-enabled network",
            blocked.Message,
            StringComparison.OrdinalIgnoreCase);

        await controller.CreateAsset(
            new CreateAssetRequestDto(
                assetCode,
                $"Master Crypto {assetCode}",
                assetCode,
                AssetType.Crypto,
                8,
                false,
                true,
                false,
                false,
                true,
                false),
            CancellationToken.None);

        var service = new DigitalAssetEnablementService(db);
        var networkCode = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var created = await service.CreateNetworkAsync(
            assetCode,
            Guid.NewGuid(),
            new CreateDigitalAssetNetworkRequestDto
            {
                NetworkCode = networkCode,
                Name = $"Master Network {networkCode}",
                NativeAssetCode = "TEST",
                Status = AssetNetworkStatus.Active,
                RequiredConfirmations = 3,
                MinimumDeposit = 1m,
                MinimumWithdrawal = 2m,
                WithdrawalFee = 0.25m,
                DepositEnabled = true,
                WithdrawalEnabled = true,
                Reason = "Create network for master-data acceptance."
            });

        Assert.Equal(assetCode, created.AssetCode);
        Assert.Equal(networkCode, created.NetworkCode);
        Assert.True(created.DepositEnabled);
        Assert.True(created.WithdrawalEnabled);

        var updated = await controller.UpdateAsset(
            assetCode,
            new UpdateAssetRequestDto(
                $"Master Crypto {assetCode}",
                assetCode,
                8,
                false,
                true,
                true,
                true,
                true,
                false),
            CancellationToken.None);

        Assert.NotNull(updated);

        var persisted = await db.Assets
            .AsNoTracking()
            .SingleAsync(x => x.Code == assetCode);

        Assert.True(persisted.DepositEnabled);
        Assert.True(persisted.WithdrawalEnabled);

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateNetworkAsync(
                assetCode,
                Guid.NewGuid(),
                new CreateDigitalAssetNetworkRequestDto
                {
                    NetworkCode = networkCode,
                    Name = "Duplicate",
                    Status = AssetNetworkStatus.Disabled,
                    DepositEnabled = false,
                    WithdrawalEnabled = false,
                    Reason = "Duplicate network rejection."
                }));

        Assert.Contains(
            "already exists",
            duplicate.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Deposit_enablement_requires_an_active_deposit_network()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Disabled,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var service = new DigitalAssetEnablementService(db);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAssetAsync(
                setup.AssetCode,
                Guid.NewGuid(),
                new UpdateDigitalAssetEnablementRequestDto
                {
                    IsSupported = true,
                    DepositEnabled = true,
                    WithdrawalEnabled = false,
                    TradingEnabled = true,
                    InstantEnabled = false,
                    Reason = "Enable deposits for test."
                }));

        Assert.Contains(
            "active deposit-enabled network",
            blocked.Message,
            StringComparison.OrdinalIgnoreCase);

        await service.UpdateNetworkAsync(
            setup.NetworkId,
            Guid.NewGuid(),
            new UpdateDigitalAssetNetworkRequestDto
            {
                Status = AssetNetworkStatus.Active,
                RequiredConfirmations = 3,
                MinimumDeposit = 1m,
                MinimumWithdrawal = 2m,
                WithdrawalFee = 0.25m,
                DepositEnabled = true,
                WithdrawalEnabled = false,
                Reason = "Prepare deposit network."
            });

        var updated = await service.UpdateAssetAsync(
            setup.AssetCode,
            Guid.NewGuid(),
            new UpdateDigitalAssetEnablementRequestDto
            {
                IsSupported = true,
                DepositEnabled = true,
                WithdrawalEnabled = false,
                TradingEnabled = true,
                InstantEnabled = false,
                Reason = "Enable deposits after network preparation."
            });

        Assert.True(updated.DepositEnabled);
        Assert.Contains(
            updated.Networks,
            x => x.Id == setup.NetworkId &&
                 x.Status == AssetNetworkStatus.Active &&
                 x.DepositEnabled);
    }

    [DatabaseIntegrationFact]
    public async Task Cannot_disable_the_only_effective_deposit_network()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: true,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Active,
            networkDepositEnabled: true,
            networkWithdrawalEnabled: false);

        var service = new DigitalAssetEnablementService(db);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateNetworkAsync(
                setup.NetworkId,
                Guid.NewGuid(),
                new UpdateDigitalAssetNetworkRequestDto
                {
                    Status = AssetNetworkStatus.Disabled,
                    RequiredConfirmations = 3,
                    MinimumDeposit = 1m,
                    MinimumWithdrawal = 2m,
                    WithdrawalFee = 0.25m,
                    DepositEnabled = false,
                    WithdrawalEnabled = false,
                    Reason = "Disable only deposit network."
                }));

        Assert.Contains(
            "Disable asset-level deposits first",
            blocked.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Unsupported_asset_requires_capabilities_and_networks_to_be_disabled()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Active,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var service = new DigitalAssetEnablementService(db);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAssetAsync(
                setup.AssetCode,
                Guid.NewGuid(),
                new UpdateDigitalAssetEnablementRequestDto
                {
                    IsSupported = false,
                    DepositEnabled = false,
                    WithdrawalEnabled = false,
                    TradingEnabled = false,
                    InstantEnabled = false,
                    Reason = "Retire asset."
                }));

        Assert.Contains(
            "Disable all digital-asset networks",
            blocked.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Administrative_updates_require_a_reason()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Disabled,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var service = new DigitalAssetEnablementService(db);

        var assetError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAssetAsync(
                setup.AssetCode,
                Guid.NewGuid(),
                new UpdateDigitalAssetEnablementRequestDto
                {
                    IsSupported = true,
                    DepositEnabled = false,
                    WithdrawalEnabled = false,
                    TradingEnabled = true,
                    InstantEnabled = false,
                    Reason = " "
                }));

        Assert.Contains(
            "reason is required",
            assetError.Message,
            StringComparison.OrdinalIgnoreCase);

        var networkError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateNetworkAsync(
                setup.NetworkId,
                Guid.NewGuid(),
                new UpdateDigitalAssetNetworkRequestDto
                {
                    Status = AssetNetworkStatus.Disabled,
                    RequiredConfirmations = 3,
                    MinimumDeposit = 1m,
                    MinimumWithdrawal = 2m,
                    WithdrawalFee = 0.25m,
                    DepositEnabled = false,
                    WithdrawalEnabled = false,
                    Reason = ""
                }));

        Assert.Contains(
            "reason is required",
            networkError.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Asset_list_returns_existing_country_availability_as_read_only_context()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Disabled,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var countryCode = $"Z{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        db.Countries.Add(new Country
        {
            Code = countryCode,
            Name = $"Digital Asset Test Country {countryCode}",
            Iso3Code = countryCode[..Math.Min(3, countryCode.Length)],
            IsSupported = true
        });

        db.CountryAssets.Add(new CountryAsset
        {
            CountryCode = countryCode,
            AssetCode = setup.AssetCode,
            CanDeposit = true,
            CanWithdraw = false,
            CanTrade = true,
            CanUseInstant = false
        });

        await db.SaveChangesAsync();

        var service = new DigitalAssetEnablementService(db);
        var assets = await service.GetAssetsAsync();

        var asset = Assert.Single(
            assets,
            x => x.Code == setup.AssetCode);

        var availability = Assert.Single(
            asset.CountryAvailability,
            x => x.CountryCode == countryCode);

        Assert.True(availability.CanDeposit);
        Assert.True(availability.CanTrade);
        Assert.False(availability.CanWithdraw);
    }

    [DatabaseIntegrationFact]
    public async Task Country_availability_admin_creates_and_updates_crypto_mapping()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Disabled,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var countryCode = $"Q{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        db.Countries.Add(new Country
        {
            Code = countryCode,
            Name = $"Digital Country {countryCode}",
            Iso3Code = countryCode[..Math.Min(3, countryCode.Length)],
            IsSupported = true
        });

        await db.SaveChangesAsync();

        var service = new DigitalAssetEnablementService(db);

        var created = await service.UpdateCountryAvailabilityAsync(
            setup.AssetCode,
            countryCode,
            Guid.NewGuid(),
            new UpdateDigitalAssetCountryAvailabilityRequestDto
            {
                CanDeposit = true,
                CanWithdraw = false,
                CanTrade = true,
                CanUseInstant = false,
                Reason = "Enable country deposit and trading."
            });

        Assert.True(created.CanDeposit);
        Assert.True(created.CanTrade);
        Assert.False(created.CanWithdraw);

        var updated = await service.UpdateCountryAvailabilityAsync(
            setup.AssetCode,
            countryCode,
            Guid.NewGuid(),
            new UpdateDigitalAssetCountryAvailabilityRequestDto
            {
                CanDeposit = false,
                CanWithdraw = true,
                CanTrade = false,
                CanUseInstant = true,
                Reason = "Switch country availability for test."
            });

        Assert.False(updated.CanDeposit);
        Assert.True(updated.CanWithdraw);
        Assert.True(updated.CanUseInstant);

        var mapping = await db.CountryAssets.AsNoTracking().SingleAsync(x =>
            x.CountryCode == countryCode &&
            x.AssetCode == setup.AssetCode);

        Assert.False(mapping.CanDeposit);
        Assert.True(mapping.CanWithdraw);
        Assert.True(mapping.CanUseInstant);
    }

    [DatabaseIntegrationFact]
    public async Task Country_availability_admin_requires_reason()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateAssetAsync(
            db,
            assetDepositEnabled: false,
            assetWithdrawalEnabled: false,
            networkStatus: AssetNetworkStatus.Disabled,
            networkDepositEnabled: false,
            networkWithdrawalEnabled: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DigitalAssetEnablementService(db)
                .UpdateCountryAvailabilityAsync(
                    setup.AssetCode,
                    "CA",
                    Guid.NewGuid(),
                    new UpdateDigitalAssetCountryAvailabilityRequestDto
                    {
                        CanDeposit = true,
                        CanWithdraw = true,
                        CanTrade = true,
                        CanUseInstant = true,
                        Reason = " "
                    }));

        Assert.Contains(
            "reason is required",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<EnablementScenario> CreateAssetAsync(
        AppDbContext db,
        bool assetDepositEnabled,
        bool assetWithdrawalEnabled,
        AssetNetworkStatus networkStatus,
        bool networkDepositEnabled,
        bool networkWithdrawalEnabled)
    {
        var assetCode = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var networkCode = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var asset = new Asset
        {
            Code = assetCode,
            Name = $"Enablement Test Asset {assetCode}",
            Symbol = assetCode,
            Type = AssetType.Crypto,
            DecimalPlaces = 8,
            IsStablecoin = true,
            IsSupported = true,
            DepositEnabled = assetDepositEnabled,
            WithdrawalEnabled = assetWithdrawalEnabled,
            TradingEnabled = true,
            InstantEnabled = false
        };

        var network = new AssetNetwork
        {
            Asset = asset,
            AssetCode = assetCode,
            NetworkCode = networkCode,
            Name = $"Enablement Test Network {networkCode}",
            NativeAssetCode = "TEST",
            RequiredConfirmations = 3,
            MinimumDeposit = 1m,
            MinimumWithdrawal = 2m,
            WithdrawalFee = 0.25m,
            DepositEnabled = networkDepositEnabled,
            WithdrawalEnabled = networkWithdrawalEnabled,
            Status = networkStatus
        };

        db.Assets.Add(asset);
        db.AssetNetworks.Add(network);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new EnablementScenario(assetCode, network.Id);
    }

    private sealed record EnablementScenario(
        string AssetCode,
        Guid NetworkId);
}