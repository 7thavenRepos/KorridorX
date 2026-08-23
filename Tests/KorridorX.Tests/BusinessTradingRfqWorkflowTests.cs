using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Marketplace;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Services.Marketplace;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessTradingRfqWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public BusinessTradingRfqWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Create_and_requote_do_not_reserve_and_previous_quote_is_superseded()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var tenant = await CreateBusinessTenantAsync(db, "rfq-requote");
        var counterparty = await AddBusinessCustomerAsync(
            db,
            tenant.ProfileId,
            "counterparty",
            "CA");

        var setup = await CreatePairAndAccountsAsync(
            db,
            operations,
            tenant.CustomerId,
            counterparty.Id,
            requesterBaseBalance: 0m,
            requesterQuoteBalance: 500m,
            counterpartyBaseBalance: 100m,
            counterpartyQuoteBalance: 0m);

        var rfq = await rfqs.CreateAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            setup.PairId,
            TradeOrderSide.Buy,
            10m,
            DateTime.UtcNow.AddMinutes(10));

        db.ChangeTracker.Clear();

        Assert.Equal(BusinessTradingRfqStatus.Open, rfq.Status);
        Assert.Equal(0, await db.FinancialReservations.CountAsync(x =>
            x.ContextEntityType == nameof(KorridorX.Models.Marketplace.BusinessTradingRfq) &&
            x.ContextEntityId == rfq.Id));

        var first = await rfqs.QuoteAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            rfq.Id,
            10m,
            DateTime.UtcNow.AddMinutes(5));

        var firstQuote = Assert.Single(first.Quotes);
        Assert.Equal(BusinessTradingRfqQuoteStatus.Active, firstQuote.Status);

        db.ChangeTracker.Clear();

        var second = await rfqs.QuoteAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            rfq.Id,
            9.50m,
            DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(BusinessTradingRfqStatus.Quoted, second.Status);
        Assert.Equal(2, second.Quotes.Count);

        var storedQuotes = await db.BusinessTradingRfqQuotes
            .AsNoTracking()
            .Where(x => x.BusinessTradingRfqId == rfq.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(BusinessTradingRfqQuoteStatus.Superseded, storedQuotes[0].Status);
        Assert.NotNull(storedQuotes[0].SupersededAt);
        Assert.Equal(BusinessTradingRfqQuoteStatus.Active, storedQuotes[1].Status);
        Assert.Equal(9.50m, storedQuotes[1].Price);

        Assert.Equal(0, await db.FinancialReservations.CountAsync(x =>
            x.ContextEntityType == nameof(KorridorX.Models.Marketplace.BusinessTradingRfq) &&
            x.ContextEntityId == rfq.Id));

        db.ChangeTracker.Clear();

        var requesterQuote = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.RequesterQuoteAccountId);
        var counterpartyBase = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.CounterpartyBaseAccountId);

        Assert.Equal(500m, requesterQuote.AvailableBalance);
        Assert.Equal(0m, requesterQuote.HeldBalance);
        Assert.Equal(100m, counterpartyBase.AvailableBalance);
        Assert.Equal(0m, counterpartyBase.HeldBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Only_selected_counterparty_can_quote_and_only_requester_can_accept_or_cancel()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var tenant = await CreateBusinessTenantAsync(db, "rfq-auth");
        var counterparty = await AddBusinessCustomerAsync(db, tenant.ProfileId, "selected", "CA");
        var stranger = await AddBusinessCustomerAsync(db, tenant.ProfileId, "stranger", "CA");

        var setup = await CreatePairAndAccountsAsync(
            db,
            operations,
            tenant.CustomerId,
            counterparty.Id,
            requesterBaseBalance: 0m,
            requesterQuoteBalance: 500m,
            counterpartyBaseBalance: 100m,
            counterpartyQuoteBalance: 0m);

        db.FinancialAccounts.AddRange(
            Account(FinancialAccountOwnerType.BusinessCustomer, stranger.Id, setup.BaseAssetCode, 100m),
            Account(FinancialAccountOwnerType.BusinessCustomer, stranger.Id, setup.QuoteAssetCode, 500m));
        await db.SaveChangesAsync();

        var rfq = await rfqs.CreateAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            setup.PairId,
            TradeOrderSide.Buy,
            10m,
            DateTime.UtcNow.AddMinutes(10));

        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            rfqs.QuoteAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                stranger.Id,
                null,
                rfq.Id,
                10m,
                DateTime.UtcNow.AddMinutes(5)));

        db.ChangeTracker.Clear();

        var quoted = await rfqs.QuoteAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            rfq.Id,
            10m,
            DateTime.UtcNow.AddMinutes(5));

        var quoteId = Assert.Single(quoted.Quotes).Id;

        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            rfqs.AcceptAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                rfq.Id,
                quoteId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            rfqs.CancelAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                rfq.Id));

        var stillQuoted = await rfqs.GetAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            rfq.Id);

        Assert.Equal(BusinessTradingRfqStatus.Quoted, stillQuoted.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Accepted_quote_settles_at_negotiated_price_and_duplicate_accept_is_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var tenant = await CreateBusinessTenantAsync(db, "rfq-settle");
        var counterparty = await AddBusinessCustomerAsync(db, tenant.ProfileId, "seller", "CA");

        var setup = await CreatePairAndAccountsAsync(
            db,
            operations,
            tenant.CustomerId,
            counterparty.Id,
            requesterBaseBalance: 0m,
            requesterQuoteBalance: 500m,
            counterpartyBaseBalance: 100m,
            counterpartyQuoteBalance: 0m);

        var rfq = await rfqs.CreateAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            setup.PairId,
            TradeOrderSide.Buy,
            10m,
            DateTime.UtcNow.AddMinutes(10));

        var quoted = await rfqs.QuoteAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            rfq.Id,
            9.50m,
            DateTime.UtcNow.AddMinutes(5));

        var quoteId = Assert.Single(quoted.Quotes).Id;

        db.ChangeTracker.Clear();

        var accepted = await rfqs.AcceptAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            null,
            rfq.Id,
            quoteId);

        Assert.Equal(BusinessTradingRfqStatus.Accepted, accepted.Status);
        Assert.Equal(quoteId, accepted.AcceptedQuoteId);
        Assert.NotNull(accepted.TradeMatchId);

        db.ChangeTracker.Clear();

        var match = await db.TradeMatches.AsNoTracking()
            .SingleAsync(x => x.Id == accepted.TradeMatchId!.Value);

        Assert.Equal(9.50m, match.Price);
        Assert.Equal(10m, match.BaseQuantity);
        Assert.Equal(95m, match.QuoteQuantity);
        Assert.Equal(TradeMatchStatus.Settled, match.Status);

        var trade = await db.Trades.AsNoTracking()
            .SingleAsync(x => x.TradeMatchId == match.Id);

        Assert.Equal(TradeStatus.Completed, trade.Status);
        Assert.Equal(9.50m, trade.Price);

        var requesterBase = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.RequesterBaseAccountId);
        var requesterQuote = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.RequesterQuoteAccountId);
        var counterpartyBase = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.CounterpartyBaseAccountId);
        var counterpartyQuote = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.CounterpartyQuoteAccountId);

        Assert.Equal(10m, requesterBase.AvailableBalance);
        Assert.Equal(10m, requesterBase.SettledBalance);
        Assert.Equal(405m, requesterQuote.AvailableBalance);
        Assert.Equal(405m, requesterQuote.SettledBalance);
        Assert.Equal(0m, requesterQuote.HeldBalance);

        Assert.Equal(90m, counterpartyBase.AvailableBalance);
        Assert.Equal(90m, counterpartyBase.SettledBalance);
        Assert.Equal(0m, counterpartyBase.HeldBalance);
        Assert.Equal(95m, counterpartyQuote.AvailableBalance);
        Assert.Equal(95m, counterpartyQuote.SettledBalance);

        var reservationCount = await db.FinancialReservations.CountAsync(x =>
            x.ContextEntityType == nameof(KorridorX.Models.Marketplace.BusinessTradingRfq) &&
            x.ContextEntityId == rfq.Id);

        Assert.Equal(2, reservationCount);

        var orderCount = await db.TradeOrders.CountAsync(x =>
            x.Id == match.BuyOrderId || x.Id == match.SellOrderId);

        Assert.Equal(2, orderCount);

        var tradeCount = await db.Trades.CountAsync(x => x.TradeMatchId == match.Id);
        Assert.Equal(1, tradeCount);

        var secondAccept = await rfqs.AcceptAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            null,
            rfq.Id,
            quoteId);

        Assert.Equal(match.Id, secondAccept.TradeMatchId);
        Assert.Equal(2, await db.FinancialReservations.CountAsync(x =>
            x.ContextEntityType == nameof(KorridorX.Models.Marketplace.BusinessTradingRfq) &&
            x.ContextEntityId == rfq.Id));
        Assert.Equal(2, await db.TradeOrders.CountAsync(x =>
            x.Id == match.BuyOrderId || x.Id == match.SellOrderId));
        Assert.Equal(1, await db.Trades.CountAsync(x => x.TradeMatchId == match.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Insufficient_balance_during_acceptance_rolls_back_orders_reservations_and_match()
    {
        Guid rfqId;
        Guid quoteId;
        Guid requesterQuoteAccountId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
            var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

            var tenant = await CreateBusinessTenantAsync(db, "rfq-insufficient");
            var counterparty = await AddBusinessCustomerAsync(db, tenant.ProfileId, "seller", "CA");

            var setup = await CreatePairAndAccountsAsync(
                db,
                operations,
                tenant.CustomerId,
                counterparty.Id,
                requesterBaseBalance: 0m,
                requesterQuoteBalance: 50m,
                counterpartyBaseBalance: 100m,
                counterpartyQuoteBalance: 0m);

            requesterQuoteAccountId = setup.RequesterQuoteAccountId;

            var rfq = await rfqs.CreateAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                tenant.CustomerId,
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                setup.PairId,
                TradeOrderSide.Buy,
                10m,
                DateTime.UtcNow.AddMinutes(10));

            rfqId = rfq.Id;

            var quoted = await rfqs.QuoteAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                rfq.Id,
                10m,
                DateTime.UtcNow.AddMinutes(5));

            quoteId = Assert.Single(quoted.Quotes).Id;

            db.ChangeTracker.Clear();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                rfqs.AcceptAsync(
                    FinancialAccountOwnerType.BusinessCustomer,
                    tenant.CustomerId,
                    null,
                    rfq.Id,
                    quoteId));

            Assert.Contains("Insufficient", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verifyScope = _fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var storedRfq = await verifyDb.BusinessTradingRfqs.AsNoTracking()
            .SingleAsync(x => x.Id == rfqId);

        Assert.Equal(BusinessTradingRfqStatus.Quoted, storedRfq.Status);
        Assert.Null(storedRfq.AcceptedQuoteId);
        Assert.Null(storedRfq.TradeMatchId);

        var storedQuote = await verifyDb.BusinessTradingRfqQuotes.AsNoTracking()
            .SingleAsync(x => x.Id == quoteId);

        Assert.Equal(BusinessTradingRfqQuoteStatus.Active, storedQuote.Status);

        Assert.Equal(0, await verifyDb.FinancialReservations.CountAsync(x =>
            x.ContextEntityType == nameof(KorridorX.Models.Marketplace.BusinessTradingRfq) &&
            x.ContextEntityId == rfqId));

        Assert.Equal(0, await verifyDb.TradeMatches.CountAsync(x =>
            x.Id == storedRfq.TradeMatchId));

        var requesterQuote = await verifyDb.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == requesterQuoteAccountId);

        Assert.Equal(50m, requesterQuote.AvailableBalance);
        Assert.Equal(50m, requesterQuote.SettledBalance);
        Assert.Equal(0m, requesterQuote.HeldBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Expired_rfq_and_active_quote_are_persisted_as_expired_before_rejection()
    {
        Guid rfqId;
        Guid quoteId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
            var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

            var tenant = await CreateBusinessTenantAsync(db, "rfq-expiry");
            var counterparty = await AddBusinessCustomerAsync(db, tenant.ProfileId, "counterparty", "CA");

            var setup = await CreatePairAndAccountsAsync(
                db,
                operations,
                tenant.CustomerId,
                counterparty.Id,
                requesterBaseBalance: 0m,
                requesterQuoteBalance: 500m,
                counterpartyBaseBalance: 100m,
                counterpartyQuoteBalance: 0m);

            var rfq = await rfqs.CreateAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                tenant.CustomerId,
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                setup.PairId,
                TradeOrderSide.Buy,
                10m,
                DateTime.UtcNow.AddMinutes(10));

            rfqId = rfq.Id;

            var quoted = await rfqs.QuoteAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                counterparty.Id,
                null,
                rfq.Id,
                10m,
                DateTime.UtcNow.AddMinutes(5));

            quoteId = Assert.Single(quoted.Quotes).Id;

            db.ChangeTracker.Clear();

            var tracked = await db.BusinessTradingRfqs
                .SingleAsync(x => x.Id == rfq.Id);
            tracked.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                rfqs.QuoteAsync(
                    FinancialAccountOwnerType.BusinessCustomer,
                    counterparty.Id,
                    null,
                    rfq.Id,
                    9.50m,
                    DateTime.UtcNow.AddMinutes(1)));

            Assert.Contains("Expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verifyScope = _fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var storedRfq = await verifyDb.BusinessTradingRfqs.AsNoTracking()
            .SingleAsync(x => x.Id == rfqId);

        var storedQuote = await verifyDb.BusinessTradingRfqQuotes.AsNoTracking()
            .SingleAsync(x => x.Id == quoteId);

        Assert.Equal(BusinessTradingRfqStatus.Expired, storedRfq.Status);
        Assert.NotNull(storedRfq.ExpiredAt);
        Assert.Equal(BusinessTradingRfqQuoteStatus.Expired, storedQuote.Status);
        Assert.NotNull(storedQuote.ExpiredAt);
    }

    [DatabaseIntegrationFact]
    public async Task Rfq_get_and_list_are_scoped_to_participants()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rfqs = scope.ServiceProvider.GetRequiredService<IBusinessTradingRfqService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var tenant = await CreateBusinessTenantAsync(db, "rfq-scope");
        var counterparty = await AddBusinessCustomerAsync(db, tenant.ProfileId, "counterparty", "CA");
        var outsider = await AddBusinessCustomerAsync(db, tenant.ProfileId, "outsider", "CA");

        var setup = await CreatePairAndAccountsAsync(
            db,
            operations,
            tenant.CustomerId,
            counterparty.Id,
            requesterBaseBalance: 0m,
            requesterQuoteBalance: 500m,
            counterpartyBaseBalance: 100m,
            counterpartyQuoteBalance: 0m);

        var rfq = await rfqs.CreateAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId,
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id,
            null,
            setup.PairId,
            TradeOrderSide.Buy,
            10m,
            DateTime.UtcNow.AddMinutes(10));

        var requesterList = await rfqs.GetForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            tenant.CustomerId);

        var counterpartyList = await rfqs.GetForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            counterparty.Id);

        Assert.Contains(requesterList.Items, x => x.Id == rfq.Id);
        Assert.Contains(counterpartyList.Items, x => x.Id == rfq.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rfqs.GetAsync(
                FinancialAccountOwnerType.BusinessCustomer,
                outsider.Id,
                rfq.Id));

        var outsiderList = await rfqs.GetForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            outsider.Id);

        Assert.DoesNotContain(outsiderList.Items, x => x.Id == rfq.Id);
    }

    private async Task<PairSetup> CreatePairAndAccountsAsync(
        AppDbContext db,
        IMarketplaceOperationsService operations,
        Guid requesterId,
        Guid counterpartyId,
        decimal requesterBaseBalance,
        decimal requesterQuoteBalance,
        decimal counterpartyBaseBalance,
        decimal counterpartyQuoteBalance)
    {
        var baseAsset = await CreateTestAssetAsync(db, "RB");
        var quoteAsset = await CreateTestAssetAsync(db, "RQ");

        var requesterBase = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            requesterId,
            baseAsset,
            requesterBaseBalance);
        var requesterQuote = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            requesterId,
            quoteAsset,
            requesterQuoteBalance);
        var counterpartyBase = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            counterpartyId,
            baseAsset,
            counterpartyBaseBalance);
        var counterpartyQuote = Account(
            FinancialAccountOwnerType.BusinessCustomer,
            counterpartyId,
            quoteAsset,
            counterpartyQuoteBalance);

        db.FinancialAccounts.AddRange(
            requesterBase,
            requesterQuote,
            counterpartyBase,
            counterpartyQuote);

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

        return new PairSetup(
            pair.Id,
            baseAsset,
            quoteAsset,
            requesterBase.Id,
            requesterQuote.Id,
            counterpartyBase.Id,
            counterpartyQuote.Id);
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
                "RfqOwner",
                $"{label}-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550131",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"RFQ Test {label} {unique}",
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
            DisplayName = $"RFQ Customer {unique}",
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
        string prefix)
    {
        var code = $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        db.Assets.Add(new Asset
        {
            Code = code,
            Name = $"RFQ Test Asset {code}",
            Symbol = code,
            DecimalPlaces = 2,
            IsSupported = true,
            TradingEnabled = true,
            InstantEnabled = false
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return code;
    }

    private static FinancialAccount Account(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        string assetCode,
        decimal balance) =>
        new()
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            AccountCode = TestReference($"ACC-{assetCode}"),
            AssetCode = assetCode,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = balance,
            AvailableBalance = balance,
            HeldBalance = 0m
        };

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record TenantFixture(
        Guid ProfileId,
        Guid CustomerId);

    private sealed record PairSetup(
        Guid PairId,
        string BaseAssetCode,
        string QuoteAssetCode,
        Guid RequesterBaseAccountId,
        Guid RequesterQuoteAccountId,
        Guid CounterpartyBaseAccountId,
        Guid CounterpartyQuoteAccountId);
}
