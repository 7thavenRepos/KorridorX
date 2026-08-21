using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Fx;
using KorridorX.Models.Lookups;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Instant;
using KorridorX.Services.Marketplace;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class EmbeddedBusinessTradingWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public EmbeddedBusinessTradingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Trading_balances_are_scoped_to_business_customer_and_tenant()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantOne = await CreateBusinessTenantAsync(db, "balance-one");
        var tenantTwo = await CreateBusinessTenantAsync(db, "balance-two");

        var asset = await CreateTestAssetAsync(db, "BAL", trading: true, instant: false);

        var ownAccount = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenantOne.CustomerId,
            FinancialAccountType.Customer,
            asset,
            125m);

        var foreignAccount = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenantTwo.CustomerId,
            FinancialAccountType.Customer,
            asset,
            999m);

        db.FinancialAccounts.AddRange(ownAccount, foreignAccount);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var trading = CreateTradingService(
            scope,
            db,
            tenantOne,
            EmbeddedFinanceScope.TradingRead);

        var balances = await trading.GetBalancesAsync(tenantOne.CustomerId);

        var row = Assert.Single(balances);
        Assert.Equal(ownAccount.Id, row.FinancialAccountId);
        Assert.Equal(asset, row.AssetCode);
        Assert.Equal(125m, row.AvailableBalance);
        Assert.Equal(125m, row.SettledBalance);
        Assert.Equal(0m, row.HeldBalance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            trading.GetBalancesAsync(tenantTwo.CustomerId));

        Assert.Contains("business customer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Business_customers_match_and_settle_through_existing_marketplace_engine()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var tenant = await CreateBusinessTenantAsync(db, "marketplace-owner");
        var buyer = await AddBusinessCustomerAsync(db, tenant.ProfileId, "marketplace-buyer", "CA");

        var baseAsset = await CreateTestAssetAsync(db, "MB", trading: true, instant: false);
        var quoteAsset = await CreateTestAssetAsync(db, "MQ", trading: true, instant: false);

        await EnableCountryTradingAsync(db, "CA", baseAsset, quoteAsset);

        var sellerBase = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountType.Customer,
            baseAsset,
            10m);

        var sellerQuote = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountType.Customer,
            quoteAsset,
            0m);

        var buyerBase = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            buyer.Id,
            FinancialAccountType.Customer,
            baseAsset,
            0m);

        var buyerQuote = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            buyer.Id,
            FinancialAccountType.Customer,
            quoteAsset,
            200m);

        db.FinancialAccounts.AddRange(
            sellerBase,
            sellerQuote,
            buyerBase,
            buyerQuote);

        await db.SaveChangesAsync();

        var pair = await operations.CreatePairAsync(
            new CreateMarketplacePairRequestDto(
                baseAsset,
                quoteAsset,
                MinimumOrderQuantity: 1m,
                MaximumOrderQuantity: 1000000m,
                QuantityIncrement: 1m,
                PriceIncrement: 0.01m,
                Status: MarketplacePairStatus.Active),
            actionedByUserId: null);

        db.ChangeTracker.Clear();

        var trading = CreateTradingService(
            scope,
            db,
            tenant,
            EmbeddedFinanceScope.TradingRead | EmbeddedFinanceScope.TradingWrite);

        var sell = await trading.CreateMarketplaceOrderAsync(
            tenant.CustomerId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 4m, 10m));

        var buy = await trading.CreateMarketplaceOrderAsync(
            buyer.Id,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 4m, 12m));

        Assert.Equal(TradeOrderStatus.Filled, buy.Status);

        db.ChangeTracker.Clear();

        var storedSell = await db.TradeOrders.AsNoTracking()
            .SingleAsync(x => x.Id == sell.Id);

        var storedBuy = await db.TradeOrders.AsNoTracking()
            .SingleAsync(x => x.Id == buy.Id);

        Assert.Equal(FinancialAccountOwnerType.BusinessCustomer, storedSell.OwnerType);
        Assert.Equal(tenant.CustomerId, storedSell.OwnerId);
        Assert.Equal(FinancialAccountOwnerType.BusinessCustomer, storedBuy.OwnerType);
        Assert.Equal(buyer.Id, storedBuy.OwnerId);
        Assert.Equal(TradeOrderStatus.Filled, storedSell.Status);
        Assert.Equal(TradeOrderStatus.Filled, storedBuy.Status);
        Assert.Equal(10m, storedBuy.AverageFillPrice);

        var sellerBaseAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == sellerBase.Id);
        var sellerQuoteAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == sellerQuote.Id);
        var buyerBaseAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == buyerBase.Id);
        var buyerQuoteAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == buyerQuote.Id);

        Assert.Equal(6m, sellerBaseAfter.AvailableBalance);
        Assert.Equal(6m, sellerBaseAfter.SettledBalance);
        Assert.Equal(40m, sellerQuoteAfter.AvailableBalance);
        Assert.Equal(40m, sellerQuoteAfter.SettledBalance);

        Assert.Equal(4m, buyerBaseAfter.AvailableBalance);
        Assert.Equal(4m, buyerBaseAfter.SettledBalance);
        Assert.Equal(160m, buyerQuoteAfter.AvailableBalance);
        Assert.Equal(160m, buyerQuoteAfter.SettledBalance);
        Assert.Equal(0m, buyerQuoteAfter.HeldBalance);

        Assert.Equal(
            1,
            await db.TradeMatches.CountAsync(x => x.MarketplacePairId == pair.Id));

        Assert.Equal(
            1,
            await db.Trades.CountAsync(x => x.MarketplacePairId == pair.Id));

        var sellReservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == storedSell.ReservationId!.Value);
        var buyReservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == storedBuy.ReservationId!.Value);

        Assert.Equal(FinancialReservationStatus.Captured, sellReservation.Status);
        Assert.Equal(4m, sellReservation.CapturedAmount);

        Assert.Equal(FinancialReservationStatus.Captured, buyReservation.Status);
        Assert.Equal(40m, buyReservation.CapturedAmount);
        Assert.Equal(8m, buyReservation.ReleasedAmount);
    }

    [DatabaseIntegrationFact]
    public async Task Business_customer_instant_trade_uses_generic_owner_and_house_liquidity()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instant = scope.ServiceProvider.GetRequiredService<IInstantTradingService>();

        var tenant = await CreateBusinessTenantAsync(db, "instant-owner");

        var sourceAsset = await CreateTestAssetAsync(db, "IS", trading: true, instant: true);
        var destinationAsset = await CreateTestAssetAsync(db, "ID", trading: true, instant: true);

        await EnableCountryInstantAsync(db, "CA", sourceAsset, destinationAsset);

        var customerSource = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountType.Customer,
            sourceAsset,
            100m);

        var customerDestination = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
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
            500m);

        db.FinancialAccounts.AddRange(
            customerSource,
            customerDestination,
            houseSource,
            houseDestination);

        db.ExchangeRates.Add(new ExchangeRate
        {
            SourceCurrencyCode = sourceAsset,
            DestinationCurrencyCode = destinationAsset,
            ProviderRate = 1.25m,
            CustomerRate = 1.25m,
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
                MinimumSourceAmount: 1m,
                MaximumSourceAmount: 1000000m,
                SourceAmountIncrement: 1m,
                QuoteValiditySeconds: 30,
                Status: InstantPairStatus.Active));

        db.ChangeTracker.Clear();

        var trading = CreateTradingService(
            scope,
            db,
            tenant,
            EmbeddedFinanceScope.TradingRead | EmbeddedFinanceScope.TradingWrite);

        var quote = await trading.CreateInstantQuoteAsync(
            tenant.CustomerId,
            new CreateInstantQuoteRequestDto(pair.Id, 20m));

        var trade = await trading.ExecuteInstantQuoteAsync(
            tenant.CustomerId,
            quote.Id);

        Assert.Equal(InstantTradeStatus.Completed, trade.Status);
        Assert.Equal(25m, trade.DestinationAmount);

        db.ChangeTracker.Clear();

        var storedQuote = await db.InstantQuotes.AsNoTracking()
            .SingleAsync(x => x.Id == quote.Id);

        var storedTrade = await db.InstantTrades.AsNoTracking()
            .SingleAsync(x => x.Id == trade.Id);

        Assert.Equal(FinancialAccountOwnerType.BusinessCustomer, storedQuote.OwnerType);
        Assert.Equal(tenant.CustomerId, storedQuote.OwnerId);
        Assert.Equal(Guid.Empty, storedQuote.UserId);

        Assert.Equal(FinancialAccountOwnerType.BusinessCustomer, storedTrade.OwnerType);
        Assert.Equal(tenant.CustomerId, storedTrade.OwnerId);
        Assert.Equal(Guid.Empty, storedTrade.UserId);

        var customerSourceAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == customerSource.Id);
        var customerDestinationAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == customerDestination.Id);
        var houseSourceAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == houseSource.Id);
        var houseDestinationAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == houseDestination.Id);

        Assert.Equal(80m, customerSourceAfter.AvailableBalance);
        Assert.Equal(80m, customerSourceAfter.SettledBalance);
        Assert.Equal(0m, customerSourceAfter.HeldBalance);

        Assert.Equal(25m, customerDestinationAfter.AvailableBalance);
        Assert.Equal(25m, customerDestinationAfter.SettledBalance);

        Assert.Equal(20m, houseSourceAfter.AvailableBalance);
        Assert.Equal(20m, houseSourceAfter.SettledBalance);

        Assert.Equal(475m, houseDestinationAfter.AvailableBalance);
        Assert.Equal(475m, houseDestinationAfter.SettledBalance);

        var reservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == storedTrade.ReservationId!.Value);

        Assert.Equal(FinancialReservationStatus.Captured, reservation.Status);
        Assert.Equal(20m, reservation.CapturedAmount);
        Assert.Equal(0m, reservation.RemainingAmount);

        Assert.Equal(
            2,
            await db.LedgerTransactions.CountAsync(x =>
                x.IdempotencyScope == "InstantTradeSettlement" &&
                x.RelatedEntityId == trade.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Trading_read_and_write_scopes_are_enforced_independently()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = await CreateBusinessTenantAsync(db, "scope-owner");

        var readOnly = CreateTradingService(
            scope,
            db,
            tenant,
            EmbeddedFinanceScope.TradingRead);

        var writeDenied = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            readOnly.CreateMarketplaceOrderAsync(
                tenant.CustomerId,
                LimitOrder(Guid.NewGuid(), TradeOrderSide.Buy, 1m, 1m)));

        Assert.Contains("TradingWrite", writeDenied.Message, StringComparison.OrdinalIgnoreCase);

        var writeOnly = CreateTradingService(
            scope,
            db,
            tenant,
            EmbeddedFinanceScope.TradingWrite);

        var readDenied = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            writeOnly.GetBalancesAsync(tenant.CustomerId));

        Assert.Contains("TradingRead", readDenied.Message, StringComparison.OrdinalIgnoreCase);
    }

    private IEmbeddedTradingService CreateTradingService(
        AsyncServiceScope scope,
        AppDbContext db,
        TenantFixture tenant,
        EmbeddedFinanceScope scopes)
    {
        return new EmbeddedTradingService(
            db,
            new FixedEmbeddedContextAccessor(new EmbeddedFinancePrincipal(
                Guid.NewGuid(),
                Guid.NewGuid(),
                tenant.ProfileId,
                scopes,
                "test-key")),
            scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>(),
            scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>(),
            scope.ServiceProvider.GetRequiredService<IInstantTradingService>());
    }

    private async Task<TenantFixture> CreateBusinessTenantAsync(
        AppDbContext db,
        string label)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Business",
                "TradingOwner",
                $"{label}-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550131",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Trading Test {label} {unique}",
            CountryCode = "CA",
            ContactEmail = $"{label}-{unique}@example.test",
            ContactPhone = "+12145550131",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var customer = new BusinessCustomer
        {
            BusinessProfile = profile,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"Trading Customer {unique}",
            Email = $"customer-{unique}@example.test",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        db.BusinessProfiles.Add(profile);
        db.BusinessCustomers.Add(customer);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new TenantFixture(profile.Id, customer.Id);
    }

    private static async Task<BusinessCustomer> AddBusinessCustomerAsync(
        AppDbContext db,
        Guid businessProfileId,
        string label,
        string countryCode)
    {
        var unique = Guid.NewGuid().ToString("N");

        var customer = new BusinessCustomer
        {
            BusinessProfileId = businessProfileId,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"{label}-{unique}",
            CountryCode = countryCode,
            Status = BusinessCustomerStatus.Active
        };

        db.BusinessCustomers.Add(customer);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return customer;
    }

    private static async Task<string> CreateTestAssetAsync(
        AppDbContext db,
        string prefix,
        bool trading,
        bool instant)
    {
        var code = $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var asset = new Asset
        {
            Code = code,
            Name = $"Test Asset {code}",
            Symbol = code,
            DecimalPlaces = 2,
            IsSupported = true,
            TradingEnabled = trading,
            InstantEnabled = instant
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return code;
    }

    private static async Task EnableCountryTradingAsync(
        AppDbContext db,
        string countryCode,
        params string[] assetCodes)
    {
        foreach (var assetCode in assetCodes)
        {
            db.CountryAssets.Add(new CountryAsset
            {
                CountryCode = countryCode,
                AssetCode = assetCode,
                CanTrade = true
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task EnableCountryInstantAsync(
        AppDbContext db,
        string countryCode,
        params string[] assetCodes)
    {
        foreach (var assetCode in assetCodes)
        {
            db.CountryAssets.Add(new CountryAsset
            {
                CountryCode = countryCode,
                AssetCode = assetCode,
                CanUseInstant = true
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
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

    private static CreateTradeOrderRequestDto LimitOrder(
        Guid pairId,
        TradeOrderSide side,
        decimal quantity,
        decimal price) =>
        new(
            pairId,
            side,
            TradeOrderType.Limit,
            quantity,
            price,
            TradeOrderTimeInForce.GoodTillCancelled);

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record TenantFixture(
        Guid ProfileId,
        Guid CustomerId);

    private sealed class FixedEmbeddedContextAccessor : IEmbeddedFinanceContextAccessor
    {
        private readonly EmbeddedFinancePrincipal _principal;

        public FixedEmbeddedContextAccessor(EmbeddedFinancePrincipal principal)
        {
            _principal = principal;
        }

        public EmbeddedFinancePrincipal GetRequiredPrincipal() => _principal;
    }
}
