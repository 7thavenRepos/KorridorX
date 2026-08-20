using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Marketplace;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Marketplace;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Marketplace;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class MarketplaceTradingWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public MarketplaceTradingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Partial_fill_settles_first_match_and_keeps_remaining_buy_reservation_active()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "USDT",
            "CAD");

        var seller = await CreateApprovedTraderAsync(
            db,
            "partial-seller",
            baseAssetCode: "USDT",
            quoteAssetCode: "CAD",
            baseBalance: 10m,
            quoteBalance: 0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "partial-buyer",
            baseAssetCode: "USDT",
            quoteAssetCode: "CAD",
            baseBalance: 0m,
            quoteBalance: 200m);

        var sell = await orders.CreateOrderAsync(
            seller.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 4m, 10m));

        var buy = await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 10m, 12m));

        Assert.Equal(TradeOrderStatus.Filled, sell.Status);
        Assert.Equal(TradeOrderStatus.PartiallyFilled, buy.Status);
        Assert.Equal(4m, buy.FilledQuantity);
        Assert.Equal(6m, buy.RemainingQuantity);
        Assert.Equal(10m, buy.AverageFillPrice);

        db.ChangeTracker.Clear();

        var buyerReservationId = await db.TradeOrders
            .Where(x => x.Id == buy.Id)
            .Select(x => x.ReservationId!.Value)
            .SingleAsync();

        var buyerReservation = await db.FinancialReservations
            .SingleAsync(x => x.Id == buyerReservationId);

        Assert.Equal(FinancialReservationStatus.Active, buyerReservation.Status);
        Assert.Equal(120m, buyerReservation.Amount);
        Assert.Equal(40m, buyerReservation.CapturedAmount);
        Assert.Equal(0m, buyerReservation.ReleasedAmount);
        Assert.Equal(80m, buyerReservation.RemainingAmount);

        var buyerQuote = await db.FinancialAccounts.SingleAsync(x => x.Id == buyer.QuoteAccountId);
        var buyerBase = await db.FinancialAccounts.SingleAsync(x => x.Id == buyer.BaseAccountId);

        Assert.Equal(80m, buyerQuote.HeldBalance);
        Assert.Equal(80m, buyerQuote.AvailableBalance);
        Assert.Equal(160m, buyerQuote.SettledBalance);
        Assert.Equal(4m, buyerBase.AvailableBalance);
        Assert.Equal(4m, buyerBase.SettledBalance);

        Assert.Equal(1, await db.TradeMatches.CountAsync(x => x.MarketplacePairId == pair.Id));
        Assert.Equal(1, await db.Trades.CountAsync(x => x.MarketplacePairId == pair.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Fully_filled_buy_order_releases_price_improvement_residual()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "USDC",
            "CAD");

        var seller = await CreateApprovedTraderAsync(
            db,
            "improvement-seller",
            "USDC",
            "CAD",
            10m,
            0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "improvement-buyer",
            "USDC",
            "CAD",
            0m,
            200m);

        await orders.CreateOrderAsync(
            seller.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 10m, 10m));

        var buy = await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 10m, 12m));

        Assert.Equal(TradeOrderStatus.Filled, buy.Status);
        Assert.Equal(10m, buy.AverageFillPrice);

        db.ChangeTracker.Clear();

        var reservationId = await db.TradeOrders
            .Where(x => x.Id == buy.Id)
            .Select(x => x.ReservationId!.Value)
            .SingleAsync();

        var reservation = await db.FinancialReservations.SingleAsync(x => x.Id == reservationId);

        Assert.Equal(120m, reservation.Amount);
        Assert.Equal(100m, reservation.CapturedAmount);
        Assert.Equal(20m, reservation.ReleasedAmount);
        Assert.Equal(0m, reservation.RemainingAmount);
        Assert.Equal(FinancialReservationStatus.Captured, reservation.Status);

        var quote = await db.FinancialAccounts.SingleAsync(x => x.Id == buyer.QuoteAccountId);
        Assert.Equal(0m, quote.HeldBalance);
        Assert.Equal(100m, quote.AvailableBalance);
        Assert.Equal(100m, quote.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Immediate_or_cancel_order_releases_full_hold_when_no_match_exists()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "EUR",
            "USD");

        var buyer = await CreateApprovedTraderAsync(
            db,
            "ioc-buyer",
            "EUR",
            "USD",
            0m,
            50m);

        var order = await orders.CreateOrderAsync(
            buyer.UserId,
            new CreateTradeOrderRequestDto(
                pair.Id,
                TradeOrderSide.Buy,
                TradeOrderType.Limit,
                5m,
                5m,
                TradeOrderTimeInForce.ImmediateOrCancel));

        Assert.Equal(TradeOrderStatus.Cancelled, order.Status);

        db.ChangeTracker.Clear();

        var storedOrder = await db.TradeOrders.SingleAsync(x => x.Id == order.Id);
        var reservation = await db.FinancialReservations.SingleAsync(
            x => x.Id == storedOrder.ReservationId!.Value);
        var quote = await db.FinancialAccounts.SingleAsync(x => x.Id == buyer.QuoteAccountId);

        Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
        Assert.Equal(25m, reservation.Amount);
        Assert.Equal(0m, reservation.CapturedAmount);
        Assert.Equal(25m, reservation.ReleasedAmount);
        Assert.Equal(0m, quote.HeldBalance);
        Assert.Equal(50m, quote.AvailableBalance);
        Assert.Equal(50m, quote.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Matching_engine_prevents_self_matching()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "GBP",
            "USD");

        var trader = await CreateApprovedTraderAsync(
            db,
            "self-match",
            "GBP",
            "USD",
            10m,
            100m);

        var sell = await orders.CreateOrderAsync(
            trader.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 2m, 10m));

        var buy = await orders.CreateOrderAsync(
            trader.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 2m, 10m));

        Assert.Equal(TradeOrderStatus.Open, sell.Status);
        Assert.Equal(TradeOrderStatus.Open, buy.Status);

        Assert.Equal(
            0,
            await db.TradeMatches.CountAsync(x => x.MarketplacePairId == pair.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Paused_pair_blocks_matching_without_releasing_existing_orders()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var matching = scope.ServiceProvider.GetRequiredService<IMarketplaceMatchingEngine>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "NGN",
            "USD");

        var seller = await CreateApprovedTraderAsync(
            db,
            "pause-seller",
            "NGN",
            "USD",
            10m,
            0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "pause-buyer",
            "NGN",
            "USD",
            0m,
            100m);

        var sell = await orders.CreateOrderAsync(
            seller.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 2m, 10m));

        var buy = await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 2m, 9m));

        await operations.SetPairStatusAsync(pair.Id, MarketplacePairStatus.Paused, null);

        db.ChangeTracker.Clear();
        var storedBuy = await db.TradeOrders.SingleAsync(x => x.Id == buy.Id);
        storedBuy.LimitPrice = 11m;
        storedBuy.LastUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var matched = await matching.MatchOrderAsync(buy.Id);

        Assert.Equal(0, matched);
        Assert.Equal(
            0,
            await db.TradeMatches.CountAsync(x => x.MarketplacePairId == pair.Id));

        var storedSell = await db.TradeOrders.AsNoTracking().SingleAsync(x => x.Id == sell.Id);
        var refreshedBuy = await db.TradeOrders.AsNoTracking().SingleAsync(x => x.Id == buy.Id);

        Assert.Equal(TradeOrderStatus.Open, storedSell.Status);
        Assert.Equal(TradeOrderStatus.Open, refreshedBuy.Status);

        var sellerReservation = await db.FinancialReservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == storedSell.ReservationId!.Value);
        var buyerReservation = await db.FinancialReservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == refreshedBuy.ReservationId!.Value);

        Assert.Equal(FinancialReservationStatus.Active, sellerReservation.Status);
        Assert.Equal(FinancialReservationStatus.Active, buyerReservation.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Repeated_settlement_of_completed_match_is_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var settlement = scope.ServiceProvider.GetRequiredService<IMarketplaceSettlementService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "USDT",
            "USDC");

        var seller = await CreateApprovedTraderAsync(
            db,
            "idempotent-seller",
            "USDT",
            "USDC",
            5m,
            0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "idempotent-buyer",
            "USDT",
            "USDC",
            0m,
            50m);

        await orders.CreateOrderAsync(
            seller.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 2m, 10m));

        await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 2m, 10m));

        db.ChangeTracker.Clear();

        var matchId = await db.TradeMatches
            .Where(x => x.MarketplacePairId == pair.Id)
            .Select(x => x.Id)
            .SingleAsync();

        var tradesBefore = await db.Trades.CountAsync(x => x.TradeMatchId == matchId);
        var settlementLedgerBefore = await db.LedgerTransactions.CountAsync(
            x => x.IdempotencyScope == "MarketplaceSettlement" &&
                 x.ContextEntityType == nameof(TradeMatch) &&
                 x.ContextEntityId == matchId);

        await settlement.SettleMatchAsync(matchId);

        db.ChangeTracker.Clear();

        var tradesAfter = await db.Trades.CountAsync(x => x.TradeMatchId == matchId);
        var settlementLedgerAfter = await db.LedgerTransactions.CountAsync(
            x => x.IdempotencyScope == "MarketplaceSettlement" &&
                 x.ContextEntityType == nameof(TradeMatch) &&
                 x.ContextEntityId == matchId);

        Assert.Equal(1, tradesBefore);
        Assert.Equal(tradesBefore, tradesAfter);
        Assert.Equal(2, settlementLedgerBefore);
        Assert.Equal(settlementLedgerBefore, settlementLedgerAfter);
    }

    [DatabaseIntegrationFact]
    public async Task Expiry_maintenance_releases_uncommitted_hold_and_marks_order_expired()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "CAD",
            "USD");

        var buyer = await CreateApprovedTraderAsync(
            db,
            "expiry-buyer",
            "CAD",
            "USD",
            0m,
            50m);

        var order = await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 2m, 5m));

        db.ChangeTracker.Clear();

        var storedOrder = await db.TradeOrders.SingleAsync(x => x.Id == order.Id);
        storedOrder.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var result = await operations.ExpireOrdersAsync();

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.ExpiredOrders);
        Assert.Equal(10m, result.ReleasedAmount);
        Assert.Empty(result.Failures);

        db.ChangeTracker.Clear();

        storedOrder = await db.TradeOrders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        var reservation = await db.FinancialReservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == storedOrder.ReservationId!.Value);
        var quote = await db.FinancialAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == buyer.QuoteAccountId);

        Assert.Equal(TradeOrderStatus.Expired, storedOrder.Status);
        Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
        Assert.Equal(10m, reservation.ReleasedAmount);
        Assert.Equal(0m, quote.HeldBalance);
        Assert.Equal(50m, quote.AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Settlement_recovery_processes_existing_matched_trade()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reservations = scope.ServiceProvider.GetRequiredService<IFinancialReservationService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "EUR",
            "CAD");

        var seller = await CreateApprovedTraderAsync(
            db,
            "recovery-seller",
            "EUR",
            "CAD",
            5m,
            0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "recovery-buyer",
            "EUR",
            "CAD",
            0m,
            50m);

        var sellerOrder = NewStoredOrder(
            pair,
            seller,
            TradeOrderSide.Sell,
            2m,
            10m);

        var buyerOrder = NewStoredOrder(
            pair,
            buyer,
            TradeOrderSide.Buy,
            2m,
            10m);

        db.TradeOrders.AddRange(sellerOrder, buyerOrder);
        await db.SaveChangesAsync();

        var sellerReservation = await reservations.ReserveAsync(
            seller.BaseAccountId,
            FinancialReservationType.MarketplaceTrade,
            nameof(TradeOrder),
            sellerOrder.Id,
            2m,
            seller.UserId,
            nameof(MarketplacePair),
            pair.Id);

        var buyerReservation = await reservations.ReserveAsync(
            buyer.QuoteAccountId,
            FinancialReservationType.MarketplaceTrade,
            nameof(TradeOrder),
            buyerOrder.Id,
            20m,
            buyer.UserId,
            nameof(MarketplacePair),
            pair.Id);

        sellerOrder.ReservationId = sellerReservation.Id;
        buyerOrder.ReservationId = buyerReservation.Id;

        var match = new TradeMatch
        {
            Reference = TestReference("MAT"),
            MarketplacePairId = pair.Id,
            BuyOrderId = buyerOrder.Id,
            SellOrderId = sellerOrder.Id,
            MakerOrderId = sellerOrder.Id,
            TakerOrderId = buyerOrder.Id,
            Price = 10m,
            BaseQuantity = 2m,
            QuoteQuantity = 20m,
            Status = TradeMatchStatus.Matched,
            MatchedAt = DateTime.UtcNow
        };

        db.TradeMatches.Add(match);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var result = await operations.RecoverSettlementsAsync();

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.RecoveredMatches);
        Assert.Empty(result.Failures);

        db.ChangeTracker.Clear();

        var storedMatch = await db.TradeMatches.AsNoTracking().SingleAsync(x => x.Id == match.Id);
        var trade = await db.Trades.AsNoTracking().SingleAsync(x => x.TradeMatchId == match.Id);
        var buyerBase = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == buyer.BaseAccountId);
        var sellerQuote = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == seller.QuoteAccountId);

        Assert.Equal(TradeMatchStatus.Settled, storedMatch.Status);
        Assert.Equal(TradeStatus.Completed, trade.Status);
        Assert.Equal(2m, buyerBase.AvailableBalance);
        Assert.Equal(2m, buyerBase.SettledBalance);
        Assert.Equal(20m, sellerQuote.AvailableBalance);
        Assert.Equal(20m, sellerQuote.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Matching_uses_price_then_time_priority()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IMarketplaceOrderService>();
        var operations = scope.ServiceProvider.GetRequiredService<IMarketplaceOperationsService>();

        var pair = await CreatePairAsync(
            operations,
            "GBP",
            "CAD");

        var sellerOne = await CreateApprovedTraderAsync(
            db,
            "priority-seller-one",
            "GBP",
            "CAD",
            2m,
            0m);

        var sellerTwo = await CreateApprovedTraderAsync(
            db,
            "priority-seller-two",
            "GBP",
            "CAD",
            2m,
            0m);

        var buyer = await CreateApprovedTraderAsync(
            db,
            "priority-buyer",
            "GBP",
            "CAD",
            0m,
            100m);

        var worsePrice = await orders.CreateOrderAsync(
            sellerOne.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 1m, 11m));

        var bestPrice = await orders.CreateOrderAsync(
            sellerTwo.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Sell, 1m, 10m));

        await orders.CreateOrderAsync(
            buyer.UserId,
            LimitOrder(pair.Id, TradeOrderSide.Buy, 1m, 12m));

        db.ChangeTracker.Clear();

        var firstMatch = await db.TradeMatches
            .AsNoTracking()
            .Where(x => x.MarketplacePairId == pair.Id)
            .OrderBy(x => x.MatchedAt)
            .ThenBy(x => x.Id)
            .FirstAsync();

        Assert.Equal(bestPrice.Id, firstMatch.SellOrderId);
        Assert.Equal(10m, firstMatch.Price);

        var worseStored = await db.TradeOrders.AsNoTracking().SingleAsync(x => x.Id == worsePrice.Id);
        Assert.Equal(TradeOrderStatus.Open, worseStored.Status);
    }

    private async Task<MarketplacePairDto> CreatePairAsync(
        IMarketplaceOperationsService operations,
        string baseAssetCode,
        string quoteAssetCode)
    {
        return await operations.CreatePairAsync(
            new CreateMarketplacePairRequestDto(
                baseAssetCode,
                quoteAssetCode,
                MinimumOrderQuantity: 1m,
                MaximumOrderQuantity: 1000000m,
                QuantityIncrement: 1m,
                PriceIncrement: 0.01m,
                Status: MarketplacePairStatus.Active),
            actionedByUserId: null);
    }

    private async Task<TraderFixture> CreateApprovedTraderAsync(
        AppDbContext db,
        string label,
        string baseAssetCode,
        string quoteAssetCode,
        decimal baseBalance,
        decimal quoteBalance)
    {
        using var client = _fixture.CreateClient();

        var email = $"{label}-{Guid.NewGuid():N}@example.test";

        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Market",
                "Tester",
                email,
                "ReleaseCandidate!123",
                "+12145550101",
                "CA",
                UserType.Consumer));

        db.ChangeTracker.Clear();

        var profile = await db.CustomerProfiles
            .SingleAsync(x => x.UserId == registration.UserId);

        profile.KycStatus = KycStatus.Approved;
        profile.KycApprovedAt = DateTime.UtcNow;

        var baseAccount = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = registration.UserId,
            AccountCode = TestReference($"ACC-{baseAssetCode}"),
            AssetCode = baseAssetCode,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = baseBalance,
            AvailableBalance = baseBalance,
            HeldBalance = 0m
        };

        var quoteAccount = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = registration.UserId,
            AccountCode = TestReference($"ACC-{quoteAssetCode}"),
            AssetCode = quoteAssetCode,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = quoteBalance,
            AvailableBalance = quoteBalance,
            HeldBalance = 0m
        };

        db.FinancialAccounts.AddRange(baseAccount, quoteAccount);
        await db.SaveChangesAsync();

        return new TraderFixture(
            registration.UserId,
            baseAccount.Id,
            quoteAccount.Id);
    }

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

    private static TradeOrder NewStoredOrder(
        MarketplacePairDto pair,
        TraderFixture trader,
        TradeOrderSide side,
        decimal quantity,
        decimal price) =>
        new()
        {
            Reference = TestReference("ORD"),
            MarketplacePairId = pair.Id,
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = trader.UserId,
            BaseFinancialAccountId = trader.BaseAccountId,
            QuoteFinancialAccountId = trader.QuoteAccountId,
            Side = side,
            OrderType = TradeOrderType.Limit,
            TimeInForce = TradeOrderTimeInForce.GoodTillCancelled,
            Status = TradeOrderStatus.Filled,
            OriginalQuantity = quantity,
            RemainingQuantity = 0m,
            FilledQuantity = quantity,
            LimitPrice = price,
            AverageFillPrice = price,
            OpenedAt = DateTime.UtcNow,
            FilledAt = DateTime.UtcNow
        };

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record TraderFixture(
        Guid UserId,
        Guid BaseAccountId,
        Guid QuoteAccountId);
}
