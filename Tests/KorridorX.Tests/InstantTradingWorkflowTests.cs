using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Instant;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Fx;
using KorridorX.Services.Instant;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class InstantTradingWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public InstantTradingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Completed_trade_moves_both_assets_atomically_and_posts_expected_ledgers()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "USD", "CAD", 100m, 500m, 1.25m);

        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 20m));

        var trade = await instant.ExecuteQuoteAsync(setup.UserId, quote.Id);

        Assert.Equal(InstantTradeStatus.Completed, trade.Status);
        Assert.Equal(25m, trade.DestinationAmount);

        db.ChangeTracker.Clear();

        var userSource = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.UserSourceAccountId);
        var userDestination = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.UserDestinationAccountId);
        var houseSource = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.HouseSourceAccountId);
        var houseDestination = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.HouseDestinationAccountId);
        var storedTrade = await db.InstantTrades.AsNoTracking().SingleAsync(x => x.Id == trade.Id);
        var reservation = await db.FinancialReservations.AsNoTracking().SingleAsync(x => x.Id == storedTrade.ReservationId!.Value);

        Assert.Equal(80m, userSource.AvailableBalance);
        Assert.Equal(80m, userSource.SettledBalance);
        Assert.Equal(0m, userSource.HeldBalance);
        Assert.Equal(25m, userDestination.AvailableBalance);
        Assert.Equal(25m, userDestination.SettledBalance);
        Assert.Equal(20m, houseSource.AvailableBalance);
        Assert.Equal(20m, houseSource.SettledBalance);
        Assert.Equal(475m, houseDestination.AvailableBalance);
        Assert.Equal(475m, houseDestination.SettledBalance);

        Assert.Equal(FinancialReservationStatus.Captured, reservation.Status);
        Assert.Equal(20m, reservation.CapturedAmount);
        Assert.Equal(0m, reservation.RemainingAmount);

        Assert.Equal(
            1,
            await db.LedgerTransactions.CountAsync(x =>
                x.Type == LedgerTransactionType.InstantReservation &&
                x.RelatedEntityId == trade.Id));

        Assert.Equal(
            2,
            await db.LedgerTransactions.CountAsync(x =>
                x.IdempotencyScope == "InstantTradeSettlement" &&
                x.RelatedEntityId == trade.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Repeated_execution_returns_same_trade_without_moving_money_twice()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "EUR", "USD", 100m, 500m, 2m);
        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 10m));

        var first = await instant.ExecuteQuoteAsync(setup.UserId, quote.Id);
        db.ChangeTracker.Clear();

        var ledgersBefore = await db.LedgerTransactions.CountAsync(x =>
            x.IdempotencyScope == "InstantTradeSettlement" &&
            x.ContextEntityId == quote.Id);

        var second = await instant.ExecuteQuoteAsync(setup.UserId, quote.Id);
        db.ChangeTracker.Clear();

        var ledgersAfter = await db.LedgerTransactions.CountAsync(x =>
            x.IdempotencyScope == "InstantTradeSettlement" &&
            x.ContextEntityId == quote.Id);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.InstantTrades.CountAsync(x => x.InstantQuoteId == quote.Id));
        Assert.Equal(2, ledgersBefore);
        Assert.Equal(ledgersBefore, ledgersAfter);
    }

    [DatabaseIntegrationFact]
    public async Task Expired_quote_persists_expired_status_and_creates_no_money_movement()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "GBP", "USD", 100m, 500m, 1.5m);
        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 10m));

        db.ChangeTracker.Clear();
        var stored = await db.InstantQuotes.SingleAsync(x => x.Id == quote.Id);
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => instant.ExecuteQuoteAsync(setup.UserId, quote.Id));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        stored = await db.InstantQuotes.AsNoTracking().SingleAsync(x => x.Id == quote.Id);

        Assert.Equal(InstantQuoteStatus.Expired, stored.Status);
        Assert.Equal(0, await db.InstantTrades.CountAsync(x => x.InstantQuoteId == quote.Id));
        Assert.Equal(0, await db.FinancialReservations.CountAsync(x => x.ContextEntityId == quote.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Liquidity_drop_after_quote_rolls_back_trade_reservation_and_ledgers()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "CAD", "USDT", 100m, 100m, 1m);
        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 50m));

        db.ChangeTracker.Clear();
        var houseDestination = await db.FinancialAccounts.SingleAsync(x => x.Id == setup.HouseDestinationAccountId);
        houseDestination.AvailableBalance = 10m;
        houseDestination.SettledBalance = 10m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => instant.ExecuteQuoteAsync(setup.UserId, quote.Id));

        db.ChangeTracker.Clear();

        var userSource = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.UserSourceAccountId);
        Assert.Equal(100m, userSource.AvailableBalance);
        Assert.Equal(0m, userSource.HeldBalance);
        Assert.Equal(100m, userSource.SettledBalance);

        Assert.Equal(0, await db.InstantTrades.CountAsync(x => x.InstantQuoteId == quote.Id));
        Assert.Equal(0, await db.FinancialReservations.CountAsync(x => x.ContextEntityId == quote.Id));
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x =>
            x.IdempotencyScope == "InstantTradeSettlement" &&
            x.ContextEntityId == quote.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Paused_pair_blocks_execution_without_consuming_quote()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "USDC", "CAD", 100m, 500m, 1m);
        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 25m));

        await instant.SetPairStatusAsync(Guid.NewGuid(), setup.PairId, InstantPairStatus.Paused);
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => instant.ExecuteQuoteAsync(setup.UserId, quote.Id));

        db.ChangeTracker.Clear();
        var stored = await db.InstantQuotes.AsNoTracking().SingleAsync(x => x.Id == quote.Id);
        var userSource = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.UserSourceAccountId);

        Assert.Equal(InstantQuoteStatus.Active, stored.Status);
        Assert.Equal(100m, userSource.AvailableBalance);
        Assert.Equal(0m, userSource.HeldBalance);
        Assert.Equal(0, await db.InstantTrades.CountAsync(x => x.InstantQuoteId == quote.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Expiry_maintenance_marks_stale_active_quote()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "NGN", "USD", 100m, 500m, 0.01m);
        var quote = await instant.CreateQuoteAsync(
            setup.UserId,
            new CreateInstantQuoteRequestDto(setup.PairId, 10m));

        db.ChangeTracker.Clear();
        var stored = await db.InstantQuotes.SingleAsync(x => x.Id == quote.Id);
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await instant.ExpireQuotesAsync();

        Assert.True(result.Candidates >= 1);
        Assert.True(result.ExpiredQuotes >= 1);
        Assert.Empty(result.Failures);

        db.ChangeTracker.Clear();
        stored = await db.InstantQuotes.AsNoTracking().SingleAsync(x => x.Id == quote.Id);
        Assert.Equal(InstantQuoteStatus.Expired, stored.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Liquidity_view_uses_house_financial_accounts()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var setup = await CreateScenarioAsync(db, instant, "USDT", "EUR", 100m, 777m, 1m);

        var rows = await instant.GetLiquidityAsync();
        var row = Assert.Single(rows.Where(x => x.InstantPairId == setup.PairId));

        Assert.Equal(setup.HouseSourceAccountId, row.HouseSourceFinancialAccountId);
        Assert.Equal(setup.HouseDestinationAccountId, row.HouseDestinationFinancialAccountId);
        Assert.Equal(777m, row.HouseDestinationAvailableBalance);
        Assert.Equal(777m, row.HouseDestinationSettledBalance);
    }

    private async Task<ScenarioFixture> CreateScenarioAsync(
        AppDbContext db,
        IInstantTradingService instant,
        string sourceAsset,
        string destinationAsset,
        decimal userSourceBalance,
        decimal houseDestinationBalance,
        decimal customerRate)
    {
        await EnableInstantAsync(db, "CA", sourceAsset, destinationAsset);

        using var client = _fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Instant",
                "Tester",
                $"instant-{Guid.NewGuid():N}@example.test",
                "ReleaseCandidate!123",
                "+12145550101",
                "CA",
                UserType.Consumer));

        db.ChangeTracker.Clear();

        var profile = await db.CustomerProfiles.SingleAsync(x => x.UserId == registration.UserId);
        profile.KycStatus = KycStatus.Approved;
        profile.KycApprovedAt = DateTime.UtcNow;

        var userSource = Account(
            FinancialAccountOwnerType.User,
            registration.UserId,
            FinancialAccountType.Customer,
            sourceAsset,
            userSourceBalance);

        var userDestination = Account(
            FinancialAccountOwnerType.User,
            registration.UserId,
            FinancialAccountType.Customer,
            destinationAsset,
            0m);

        var houseOwnerId = Guid.NewGuid();

        var houseSource = Account(
            FinancialAccountOwnerType.Treasury,
            houseOwnerId,
            FinancialAccountType.House,
            sourceAsset,
            0m);

        var houseDestination = Account(
            FinancialAccountOwnerType.Treasury,
            houseOwnerId,
            FinancialAccountType.House,
            destinationAsset,
            houseDestinationBalance);

        db.FinancialAccounts.AddRange(
            userSource,
            userDestination,
            houseSource,
            houseDestination);

        db.ExchangeRates.Add(new ExchangeRate
        {
            SourceCurrencyCode = sourceAsset,
            DestinationCurrencyCode = destinationAsset,
            ProviderRate = customerRate,
            CustomerRate = customerRate,
            MarkupRate = 0m,
            ProviderCode = "TEST",
            ProviderRateId = TestReference("RATE"),
            EffectiveFrom = DateTime.UtcNow,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var pair = await instant.CreatePairAsync(
            Guid.NewGuid(),
            new CreateInstantPairRequestDto(
                sourceAsset,
                destinationAsset,
                houseSource.Id,
                houseDestination.Id,
                1m,
                1000000m,
                1m,
                30,
                InstantPairStatus.Active));

        db.ChangeTracker.Clear();

        return new ScenarioFixture(
            registration.UserId,
            pair.Id,
            userSource.Id,
            userDestination.Id,
            houseSource.Id,
            houseDestination.Id);
    }

    private static FinancialAccount Account(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        FinancialAccountType accountType,
        string assetCode,
        decimal balance) =>
        new()
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            AccountCode = TestReference($"ACC-{assetCode}"),
            AssetCode = assetCode,
            AccountType = accountType,
            Status = FinancialAccountStatus.Active,
            SettledBalance = balance,
            AvailableBalance = balance,
            HeldBalance = 0m
        };

    private static async Task EnableInstantAsync(
        AppDbContext db,
        string countryCode,
        params string[] assetCodes)
    {
        var mappings = await db.CountryAssets
            .Where(x => x.CountryCode == countryCode && assetCodes.Contains(x.AssetCode))
            .ToListAsync();

        foreach (var mapping in mappings)
            mapping.CanUseInstant = true;

        var assets = await db.Assets
            .Where(x => assetCodes.Contains(x.Code))
            .ToListAsync();

        foreach (var asset in assets)
            asset.InstantEnabled = true;

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record ScenarioFixture(
        Guid UserId,
        Guid PairId,
        Guid UserSourceAccountId,
        Guid UserDestinationAccountId,
        Guid HouseSourceAccountId,
        Guid HouseDestinationAccountId);
}
