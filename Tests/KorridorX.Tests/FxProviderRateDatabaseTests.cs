using KorridorX.Data;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Fx;
using KorridorX.Models.Lookups;
using KorridorX.Models.Marketplace;
using KorridorX.Providers.Fx;
using KorridorX.Services.Audit;
using KorridorX.Services.Fx;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace KorridorX.Tests;

// These tests use only a new random schema in the explicitly supplied disposable RC database.
// They never start the API, resolve real providers, or touch public-schema wallets or payment handlers.
public sealed class FxProviderRateDatabaseFixture : IAsyncLifetime
{
    private readonly string _schema = "kx_fx_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = "";
    public async Task InitializeAsync()
    {
        if (!DatabaseIntegrationTestEnvironment.IsConfigured) return;
        var cs = new NpgsqlConnectionStringBuilder(DatabaseIntegrationTestEnvironment.ConnectionString) { SearchPath = _schema };
        ConnectionString = cs.ConnectionString;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await command.ExecuteNonQueryAsync();
        await using var db = CreateDb();
        await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
    public AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
    public async Task DisposeAsync()
    {
        if (ConnectionString.Length == 0) return;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("FxProviderRatesDatabase", DisableParallelization = true)]
public sealed class FxProviderRateDatabaseCollection : ICollectionFixture<FxProviderRateDatabaseFixture> { }

[Collection("FxProviderRatesDatabase")]
public sealed class FxProviderRateDatabaseTests(FxProviderRateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Sequential_new_quotes_each_read_the_provider_and_keep_real_provider_time()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        var first = await QuoteAsync(db, service, scenario);
        var second = await QuoteAsync(db, service, scenario);
        Assert.Equal(3, scenario.Provider.Calls); // activation + both requests; no completed-fetch cache
        Assert.NotEqual(first.FxRateObservationId, second.FxRateObservationId);
        var observations = await db.FxRateObservations.Where(x => x.ConfigurationId == scenario.Configuration.Id).ToListAsync();
        Assert.Equal(3, observations.Count);
        Assert.All(observations, x => Assert.Equal(scenario.Provider.ProviderTime, x.ProviderTimestampUtc));
        Assert.Equal(0, await db.LedgerTransactions.CountAsync());
        Assert.Equal(0, await db.FinancialAccounts.CountAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Provider_failure_never_uses_a_stored_successful_rate()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var previous = await db.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        scenario.Provider.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => QuoteAsync(db, Service(db, scenario.Provider), scenario));
        var current = await db.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        Assert.Equal(previous.LastSuccessAtUtc, current.LastSuccessAtUtc);
        Assert.NotNull(current.LastError);
        Assert.False(await db.TransferQuotes.AnyAsync(x => x.SourceCurrencyCode == scenario.Source));
        Assert.Equal(1, await db.FxRateObservations.CountAsync(x => x.ConfigurationId == scenario.Configuration.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Failed_activation_keeps_previous_configuration_active()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        var next = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 3m));
        scenario.Provider.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateAsync(Guid.Empty, next.Id, next.Revision));
        Assert.Equal(scenario.Configuration.Id, (await db.FxProviderRateConfigurations.AsNoTracking()
            .SingleAsync(x => x.SourceCurrencyCode == scenario.Source && x.IsActive)).Id);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_activations_leave_exactly_one_active_direction()
    {
        var scenario = await ScenarioAsync();
        FxProviderConfigurationDto one, two;
        await using (var db = fixture.CreateDb())
        {
            var service = Service(db, scenario.Provider);
            one = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 3m));
            two = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 4m));
        }
        await using var first = fixture.CreateDb();
        await using var second = fixture.CreateDb();
        await Task.WhenAll(Service(first, scenario.Provider).ActivateAsync(Guid.Empty, one.Id, one.Revision),
            Service(second, scenario.Provider).ActivateAsync(Guid.Empty, two.Id, two.Revision));
        await using var verify = fixture.CreateDb();
        Assert.Equal(1, await verify.FxProviderRateConfigurations.CountAsync(x => x.SourceCurrencyCode == scenario.Source && x.IsActive));
        Assert.Equal(1, await verify.ExchangeRates.CountAsync(x => x.SourceCurrencyCode == scenario.Source && x.IsActive));
    }

    [DatabaseIntegrationFact]
    public async Task Database_constraint_rejects_a_second_active_configuration_without_service_guards()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        db.FxProviderRateConfigurations.Add(new FxProviderRateConfiguration
        {
            MarketplacePairId = scenario.PairId, SourceCurrencyCode = scenario.Source,
            DestinationCurrencyCode = scenario.Destination, ProviderCode = "AnotherProvider", IsActive = true
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (error.InnerException as PostgresException)?.SqlState);
    }

    [DatabaseIntegrationFact]
    public async Task A_to_B_to_A_switch_rejects_the_original_in_flight_quote()
    {
        var scenario = await ScenarioAsync();
        var block = scenario.Provider.BlockNext();
        await using var quoting = fixture.CreateDb();
        var pending = QuoteAsync(quoting, Service(quoting, scenario.Provider), scenario);
        await block.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await using var admin = fixture.CreateDb();
            var service = Service(admin, scenario.Provider);
            var replacement = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 3m));
            await service.ActivateAsync(Guid.Empty, replacement.Id, replacement.Revision);
            var original = await admin.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
            await service.ActivateAsync(Guid.Empty, original.Id, original.Revision);
        }
        finally { block.Release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.False(await quoting.TransferQuotes.AnyAsync(x => x.SourceCurrencyCode == scenario.Source));
    }

    [DatabaseIntegrationFact]
    public async Task Connection_rotation_rejects_the_old_response_and_requires_reactivation()
    {
        var scenario = await ScenarioAsync();
        var block = scenario.Provider.BlockNext();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        var pending = QuoteAsync(db, service, scenario);
        await block.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        scenario.Provider.ConnectionKey = new string('b', 64);
        block.Release.TrySetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => QuoteAsync(db, service, scenario));
        var config = await db.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        await service.ActivateAsync(Guid.Empty, config.Id, config.Revision);
        Assert.NotNull((await QuoteAsync(db, service, scenario)).FxRateObservationId);
    }

    [DatabaseIntegrationFact]
    public async Task A_late_older_response_cannot_overwrite_a_newer_successful_observation()
    {
        var scenario = await ScenarioAsync();
        var block = scenario.Provider.BlockNext();
        await using var olderDb = fixture.CreateDb();
        var pending = QuoteAsync(olderDb, Service(olderDb, scenario.Provider), scenario);
        await block.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var newerDb = fixture.CreateDb();
        scenario.Provider.Rate = 200m;
        var newer = await QuoteAsync(newerDb, Service(newerDb, scenario.Provider), scenario);
        block.Release.TrySetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        var config = await newerDb.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        Assert.Equal(200m, config.LastProviderRate);
        Assert.Equal(196m, newer.CustomerRate);
    }

    [DatabaseIntegrationFact]
    public async Task Existing_quote_economics_and_expiry_survive_refresh_and_provider_switch()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        var quote = await QuoteAsync(db, service, scenario);
        var expiry = await db.TransferQuotes.AsNoTracking().Where(x => x.Id == quote.Id).Select(x => x.ExpiresAt).SingleAsync();
        var observation = quote.FxRateObservationId;
        scenario.Provider.Rate = 250m;
        var replacement = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 4m));
        await service.ActivateAsync(Guid.Empty, replacement.Id, replacement.Revision);
        db.ChangeTracker.Clear();
        var stored = await db.TransferQuotes.SingleAsync(x => x.Id == quote.Id);
        Assert.Equal(98m, stored.CustomerRate);
        Assert.Equal(100m, stored.ProviderRate);
        Assert.Equal(980m, stored.DestinationAmount);
        Assert.Equal(expiry, stored.ExpiresAt);
        Assert.Equal(observation, stored.FxRateObservationId);
        Assert.False(stored.IsUsed);
    }

    [DatabaseIntegrationFact]
    public async Task Callback_failure_rolls_back_quote_observation_rate_and_tracked_changes()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UseFreshRateAsync<int>(scenario.Source, scenario.Destination,
            FxRateUse.Transfer, (rate, observation, ct) =>
            {
                db.TransferQuotes.Add(new TransferQuote { SourceCurrencyCode = scenario.Source, FxRateObservationId = observation });
                throw new InvalidOperationException("Consumer validation failed");
            }));
        await db.SaveChangesAsync(); // A caller catching validation cannot accidentally flush rolled-back FX rows.
        Assert.Equal(1, await db.FxRateObservations.CountAsync(x => x.ConfigurationId == scenario.Configuration.Id));
        Assert.False(await db.TransferQuotes.AnyAsync(x => x.SourceCurrencyCode == scenario.Source));
    }

    [DatabaseIntegrationFact]
    public async Task Inactive_refresh_records_preview_without_publishing_or_replacing_active_provider()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        var draft = await service.SaveAsync(Guid.Empty, null, Request(scenario.PairId, 8m));
        var checkedDraft = await service.RefreshAsync(Guid.Empty, draft.Id, draft.Revision);
        Assert.False(checkedDraft.IsActive);
        Assert.Equal(92m, checkedDraft.LastCustomerRate);
        Assert.Null(checkedDraft.NextRefreshAtUtc);
        Assert.False(await db.ExchangeRates.AnyAsync(x => x.FxProviderRateConfigurationId == draft.Id));
        Assert.True(await db.FxProviderRateConfigurations.AnyAsync(x => x.Id == scenario.Configuration.Id && x.IsActive));
    }

    [DatabaseIntegrationFact]
    public async Task Scheduled_failure_keeps_last_success_and_retries_without_switching_provider()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        await db.FxProviderRateConfigurations.Where(x => x.Id == scenario.Configuration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.NextRefreshAtUtc, (DateTime?)DateTime.UtcNow.AddMinutes(-1)));
        var before = await db.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        scenario.Provider.Fail = true;
        await Service(db, scenario.Provider).RefreshDueAsync();
        var after = await db.FxProviderRateConfigurations.AsNoTracking().SingleAsync(x => x.Id == scenario.Configuration.Id);
        Assert.True(after.IsActive);
        Assert.Equal(before.LastSuccessAtUtc, after.LastSuccessAtUtc);
        Assert.Equal(before.LastProviderTimestampUtc, after.LastProviderTimestampUtc);
        Assert.NotNull(after.LastError);
        Assert.Null(after.RefreshLeaseId);
        Assert.True(after.NextRefreshAtUtc > DateTime.UtcNow);
    }

    [DatabaseIntegrationFact]
    public async Task Required_provider_capability_is_checked_without_falling_back()
    {
        var scenario = await ScenarioAsync();
        await using var db = fixture.CreateDb();
        var service = Service(db, scenario.Provider);
        scenario.Provider.SendEnabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => QuoteAsync(db, service, scenario));
        var rate = await service.UseFreshRateAsync(scenario.Source, scenario.Destination, FxRateUse.Instant,
            (value, _, _) => Task.FromResult(value.CustomerRate));
        Assert.Equal(98m, rate);
    }

    private FxProviderRateService Service(AppDbContext db, FakeProvider provider) =>
        new(db, [provider], new AuditService(db, new HttpContextAccessor()), NullLogger<FxProviderRateService>.Instance);

    private async Task<Scenario> ScenarioAsync()
    {
        await using var db = fixture.CreateDb();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var source = "S" + suffix; var destination = "D" + suffix;
        db.Assets.AddRange(new Asset { Code = source, Name = source, Symbol = source },
            new Asset { Code = destination, Name = destination, Symbol = destination });
        var pair = new MarketplacePair { Code = source + "/" + destination, BaseAssetCode = source, QuoteAssetCode = destination,
            MinimumOrderQuantity = 1, QuantityIncrement = 1, PriceIncrement = 0.01m };
        db.MarketplacePairs.Add(pair); await db.SaveChangesAsync();
        var provider = new FakeProvider(); var service = Service(db, provider);
        var draft = await service.SaveAsync(Guid.Empty, null, Request(pair.Id, 2m));
        var active = await service.ActivateAsync(Guid.Empty, draft.Id, draft.Revision);
        return new(pair.Id, source, destination, active, provider);
    }

    private static SaveFxProviderConfigurationRequestDto Request(Guid pairId, decimal markup) => new()
    { PairType = "Marketplace", PairId = pairId, ProviderCode = "TestProvider", MarkupPercentage = markup };

    private static Task<TransferQuote> QuoteAsync(AppDbContext db, ILiveFxRateService service, Scenario scenario) =>
        service.UseFreshRateAsync(scenario.Source, scenario.Destination, FxRateUse.Transfer, (rate, observation, ct) =>
        {
            var quote = new TransferQuote { SourceCurrencyCode = scenario.Source, DestinationCurrencyCode = scenario.Destination,
                SourceCountryCode = "CA", DestinationCountryCode = "NG", SourceAmount = 10m,
                DestinationAmount = 10m * rate.CustomerRate, ProviderRate = rate.ProviderRate, CustomerRate = rate.CustomerRate,
                ProviderCode = rate.ProviderCode, FxRateObservationId = observation, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
            db.TransferQuotes.Add(quote); return Task.FromResult(quote);
        });

    private sealed record Scenario(Guid PairId, string Source, string Destination, FxProviderConfigurationDto Configuration, FakeProvider Provider);
    private sealed class PendingRead
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class FakeProvider : IFxRateProvider
    {
        private int _calls;
        private PendingRead? _pending;
        public string ProviderCode => "TestProvider";
        public string ConnectionKey { get; set; } = new('a', 64);
        public bool IsAvailable => true;
        public bool Fail { get; set; }
        public bool SendEnabled { get; set; } = true;
        public decimal Rate { get; set; } = 100m;
        public DateTime ProviderTime { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public int Calls => _calls;
        public PendingRead BlockNext() { var block = new PendingRead(); _pending = block; return block; }
        public async Task<FxProviderRate> GetRateAsync(string sourceCurrencyCode, string destinationCurrencyCode, string? providerRateId = null, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            if (Fail) throw new InvalidOperationException("Simulated provider unavailable");
            var started = DateTime.UtcNow; var key = ConnectionKey; var value = Rate;
            var block = Interlocked.Exchange(ref _pending, null);
            if (block is not null) { block.Entered.TrySetResult(); await block.Release.Task.WaitAsync(ct); }
            return new(sourceCurrencyCode, destinationCurrencyCode, value, providerRateId ?? "123", ProviderTime,
                started, DateTime.UtcNow, key, Guid.NewGuid(), SendEnabled, true);
        }
    }
}
