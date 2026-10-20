using KorridorX.Data;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Instant;
using KorridorX.Models.Lookups;
using KorridorX.Models.Marketplace;
using KorridorX.Services.Audit;
using KorridorX.Services.Fx;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace KorridorX.Tests;

// Isolated schema in the explicitly configured disposable RC database. No API,
// provider, notification worker or real wallet is resolved or contacted.
public sealed class TransferFeeDatabaseFixture : IAsyncLifetime
{
    private readonly string schema = "kx_fee_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = "";
    public async Task InitializeAsync()
    {
        if (!DatabaseIntegrationTestEnvironment.IsConfigured) return;
        ConnectionString = new NpgsqlConnectionStringBuilder(DatabaseIntegrationTestEnvironment.ConnectionString) { SearchPath = schema }.ConnectionString;
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection); await command.ExecuteNonQueryAsync();
        await using var db = CreateDb(); await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
    public AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
    public async Task DisposeAsync()
    {
        if (ConnectionString.Length == 0) return;
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection); await command.ExecuteNonQueryAsync();
    }
}
[CollectionDefinition("TransferFeesDatabase", DisableParallelization = true)]
public sealed class TransferFeeDatabaseCollection : ICollectionFixture<TransferFeeDatabaseFixture> { }

[Collection("TransferFeesDatabase")]
public sealed class AdminTransferFeeDatabaseTests(TransferFeeDatabaseFixture fixture)
{
    private static AdminTransferFeeService Service(AppDbContext db) => new(db, new AuditService(db, new HttpContextAccessor()));

    [DatabaseIntegrationFact]
    public async Task Saving_an_explicit_zero_fee_creates_an_inactive_audited_rule_in_source_currency()
    {
        var request = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var row = await Service(db).SaveAsync(Guid.NewGuid(), null, request);
        Assert.False(row.IsActive); Assert.Equal(request.SourceCurrencyCode, row.FeeCurrencyCode);
        Assert.Equal(0, row.FixedFee); Assert.Equal(0, row.PercentageFee);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == row.Id.ToString() && x.Action == "TransferFeeCreated"));
        Assert.False(await db.LedgerTransactions.AnyAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Activation_makes_a_matching_fee_available_and_deactivation_removes_it()
    {
        var request = await ScenarioAsync(); request.FixedFee = 1; request.PercentageFee = 1;
        await using var db = fixture.CreateDb(); var service = Service(db);
        var row = await service.SaveAsync(Guid.NewGuid(), null, request);
        row = await service.SetActiveAsync(Guid.NewGuid(), row.Id, row.Revision, true);
        // Same matching predicate as the consumer quote path; no transfer is created.
        var fee = await db.TransferFees.AsNoTracking().SingleAsync(x => x.SourceCountryCode == request.SourceCountryCode &&
            x.DestinationCountryCode == request.DestinationCountryCode && x.SourceCurrencyCode == request.SourceCurrencyCode &&
            x.DestinationCurrencyCode == request.DestinationCurrencyCode && x.TransferType == request.TransferType &&
            x.IsActive && 5 >= x.MinAmount && (x.MaxAmount == null || 5 <= x.MaxAmount));
        Assert.Equal(1.05m, fee.FixedFee + decimal.Round(5 * fee.PercentageFee / 100, 2));
        await service.SetActiveAsync(Guid.NewGuid(), row.Id, row.Revision, false);
        Assert.False(await db.TransferFees.AnyAsync(x => x.Id == row.Id && x.IsActive));
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_overlapping_activations_leave_only_one_active_rule()
    {
        var request = await ScenarioAsync();
        AdminTransferFeeDto first, second;
        await using (var db = fixture.CreateDb())
        { first = await Service(db).SaveAsync(Guid.NewGuid(), null, request); second = await Service(db).SaveAsync(Guid.NewGuid(), null, request); }
        await using var one = fixture.CreateDb(); await using var two = fixture.CreateDb();
        var results = await Task.WhenAll(Activate(Service(one), first), Activate(Service(two), second));
        Assert.Single(results.Where(x => x));
        await using var verify = fixture.CreateDb();
        Assert.Equal(1, await verify.TransferFees.CountAsync(x => x.SourceCurrencyCode == request.SourceCurrencyCode && x.IsActive));
        static async Task<bool> Activate(AdminTransferFeeService service, AdminTransferFeeDto row)
        { try { await service.SetActiveAsync(Guid.NewGuid(), row.Id, row.Revision, true); return true; } catch (InvalidOperationException) { return false; } }
    }

    [DatabaseIntegrationFact]
    public async Task Nonoverlapping_ranges_can_be_active_and_a_touching_boundary_is_rejected()
    {
        var request = await ScenarioAsync(); request.MaxAmount = 100;
        await using var db = fixture.CreateDb(); var service = Service(db);
        var first = await service.SaveAsync(Guid.NewGuid(), null, request);
        await service.SetActiveAsync(Guid.NewGuid(), first.Id, first.Revision, true);
        request.MinAmount = 100; request.MaxAmount = null;
        var second = await service.SaveAsync(Guid.NewGuid(), null, request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(Guid.NewGuid(), second.Id, second.Revision, true));
        request.MinAmount = 100.01m; request.Revision = second.Revision;
        second = await service.SaveAsync(Guid.NewGuid(), second.Id, request);
        await service.SetActiveAsync(Guid.NewGuid(), second.Id, second.Revision, true);
        Assert.Equal(2, await db.TransferFees.CountAsync(x => x.SourceCurrencyCode == request.SourceCurrencyCode && x.IsActive));
    }

    [DatabaseIntegrationFact]
    public async Task Stale_revisions_and_edits_of_active_fees_are_rejected()
    {
        var request = await ScenarioAsync();
        await using var db = fixture.CreateDb(); var service = Service(db);
        var row = await service.SaveAsync(Guid.NewGuid(), null, request);
        request.Revision = row.Revision; request.FixedFee = 1;
        var updated = await service.SaveAsync(Guid.NewGuid(), row.Id, request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), row.Id, request));
        updated = await service.SetActiveAsync(Guid.NewGuid(), row.Id, updated.Revision, true);
        request.Revision = updated.Revision;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), row.Id, request));
    }

    [DatabaseIntegrationFact]
    public async Task Unsupported_country_mapping_or_missing_direction_cannot_be_saved()
    {
        var request = await ScenarioAsync();
        await using var db = fixture.CreateDb(); var service = Service(db);
        var original = request.SourceCountryCode; request.SourceCountryCode = request.DestinationCountryCode;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), null, request));
        request.SourceCountryCode = original;
        var pair = await db.MarketplacePairs.SingleAsync(x => x.BaseAssetCode == request.SourceCurrencyCode);
        pair.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), null, request));
    }

    [DatabaseIntegrationFact]
    public async Task Business_to_personal_fees_follow_business_product_asset_eligibility_on_both_sides()
    {
        // Both existing business quote paths require BusinessEnabled on the payout
        // asset too; the recipient does not need to own a KorridorX personal wallet.
        var request = await ScenarioAsync(); request.TransferType = TransferType.BusinessToConsumer;
        await using var db = fixture.CreateDb(); var service = Service(db);
        var destination = await db.Assets.SingleAsync(x => x.Code == request.DestinationCurrencyCode);
        destination.BusinessEnabled = false; destination.IndividualEnabled = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), null, request));
        destination.BusinessEnabled = true; destination.IndividualEnabled = false; await db.SaveChangesAsync();
        var row = await service.SaveAsync(Guid.NewGuid(), null, request);
        Assert.Equal(TransferType.BusinessToConsumer, row.TransferType);
        request.TransferType = TransferType.ConsumerToConsumer;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), null, request));
    }

    [DatabaseIntegrationFact]
    public async Task A_paused_pair_can_be_drafted_but_cannot_be_activated()
    {
        var request = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var pair = await db.MarketplacePairs.SingleAsync(x => x.BaseAssetCode == request.SourceCurrencyCode);
        pair.Status = MarketplacePairStatus.Paused; await db.SaveChangesAsync();
        var service = Service(db); var row = await service.SaveAsync(Guid.NewGuid(), null, request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(Guid.NewGuid(), row.Id, row.Revision, true));
    }

    [DatabaseIntegrationFact]
    public async Task Options_show_one_direction_when_it_exists_in_both_pair_catalogs()
    {
        var request = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var from = new FinancialAccount { OwnerType = FinancialAccountOwnerType.Platform, OwnerId = Guid.NewGuid(),
            AccountCode = Guid.NewGuid().ToString("N"), AssetCode = request.SourceCurrencyCode };
        var to = new FinancialAccount { OwnerType = FinancialAccountOwnerType.Platform, OwnerId = Guid.NewGuid(),
            AccountCode = Guid.NewGuid().ToString("N"), AssetCode = request.DestinationCurrencyCode };
        db.FinancialAccounts.AddRange(from, to);
        db.InstantPairs.Add(new InstantPair { Code = Guid.NewGuid().ToString("N"), SourceAssetCode = request.SourceCurrencyCode,
            DestinationAssetCode = request.DestinationCurrencyCode, HouseSourceFinancialAccount = from, HouseDestinationFinancialAccount = to,
            SourceAmountIncrement = 0.01m, Status = InstantPairStatus.Active });
        await db.SaveChangesAsync();
        var options = await Service(db).GetOptionsAsync();
        Assert.Single(options.Pairs.Where(x => x.SourceCurrencyCode == request.SourceCurrencyCode));
    }

    private async Task<SaveTransferFeeRequestDto> ScenarioAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        var from = "S" + suffix; var to = "D" + suffix;
        await using var db = fixture.CreateDb();
        db.Assets.AddRange(new Asset { Code = from, Name = from, Symbol = from }, new Asset { Code = to, Name = to, Symbol = to });
        db.Countries.AddRange(new Country { Code = from, Name = from, IsSendCountry = true }, new Country { Code = to, Name = to, IsReceiveCountry = true });
        db.CountryAssets.AddRange(new CountryAsset { CountryCode = from, AssetCode = from, CanSend = true },
            new CountryAsset { CountryCode = to, AssetCode = to, CanReceive = true });
        db.MarketplacePairs.Add(new MarketplacePair { Code = from + "/" + to, BaseAssetCode = from, QuoteAssetCode = to,
            MinimumOrderQuantity = 1, QuantityIncrement = 1, PriceIncrement = 0.01m });
        await db.SaveChangesAsync();
        return new() { SourceCountryCode = from, DestinationCountryCode = to, SourceCurrencyCode = from, DestinationCurrencyCode = to,
            TransferType = TransferType.ConsumerToConsumer, MinAmount = 0, FixedFee = 0, PercentageFee = 0 };
    }
}
