using KorridorX.Data;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Marketplace;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Marketplace;

public class MarketplaceOrderService : IMarketplaceOrderService
{
    private readonly AppDbContext _db;
    private readonly IFinancialReservationService _reservationService;
    private readonly IMarketplaceMatchingEngine _matchingEngine;

    public MarketplaceOrderService(
        AppDbContext db,
        IFinancialReservationService reservationService,
        IMarketplaceMatchingEngine matchingEngine)
    {
        _db = db;
        _reservationService = reservationService;
        _matchingEngine = matchingEngine;
    }

    public async Task<IReadOnlyList<MarketplacePairDto>> GetActivePairsAsync(CancellationToken ct = default)
    {
        return await _db.MarketplacePairs
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == MarketplacePairStatus.Active)
            .OrderBy(x => x.Code)
            .Select(x => new MarketplacePairDto(
                x.Id,
                x.Code,
                x.BaseAssetCode,
                x.QuoteAssetCode,
                x.Status,
                x.MinimumOrderQuantity,
                x.MaximumOrderQuantity,
                x.QuantityIncrement,
                x.PriceIncrement))
            .ToListAsync(ct);
    }

    public async Task<OrderBookDto> GetOrderBookAsync(
        Guid marketplacePairId,
        int depth = 20,
        CancellationToken ct = default)
    {
        depth = Math.Clamp(depth, 1, 100);

        var pair = await _db.MarketplacePairs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == marketplacePairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Marketplace pair not found.");

        var activeStatuses = new[] { TradeOrderStatus.Open, TradeOrderStatus.PartiallyFilled };
        var now = DateTime.UtcNow;

        var bids = await _db.TradeOrders
            .AsNoTracking()
            .Where(x =>
                x.MarketplacePairId == marketplacePairId &&
                x.Side == TradeOrderSide.Buy &&
                activeStatuses.Contains(x.Status) &&
                x.RemainingQuantity > 0m &&
                (!x.ExpiresAt.HasValue || x.ExpiresAt > now) &&
                !x.IsDeleted &&
                x.LimitPrice != null)
            .GroupBy(x => x.LimitPrice!.Value)
            .Select(g => new OrderBookLevelDto(g.Key, g.Sum(x => x.RemainingQuantity), g.Count()))
            .OrderByDescending(x => x.Price)
            .Take(depth)
            .ToListAsync(ct);

        var asks = await _db.TradeOrders
            .AsNoTracking()
            .Where(x =>
                x.MarketplacePairId == marketplacePairId &&
                x.Side == TradeOrderSide.Sell &&
                activeStatuses.Contains(x.Status) &&
                x.RemainingQuantity > 0m &&
                (!x.ExpiresAt.HasValue || x.ExpiresAt > now) &&
                !x.IsDeleted &&
                x.LimitPrice != null)
            .GroupBy(x => x.LimitPrice!.Value)
            .Select(g => new OrderBookLevelDto(g.Key, g.Sum(x => x.RemainingQuantity), g.Count()))
            .OrderBy(x => x.Price)
            .Take(depth)
            .ToListAsync(ct);

        return new OrderBookDto(pair.Id, pair.Code, bids, asks, now);
    }

    public async Task<TradeOrderDto> CreateOrderAsync(
        Guid userId,
        CreateTradeOrderRequestDto request,
        CancellationToken ct = default)
    {
        if (request.OrderType != TradeOrderType.Limit)
        {
            throw new InvalidOperationException("Only limit orders are enabled in the initial marketplace release.");
        }

        if (request.TimeInForce is not TradeOrderTimeInForce.GoodTillCancelled and not TradeOrderTimeInForce.ImmediateOrCancel)
        {
            throw new InvalidOperationException("Only good-till-cancelled and immediate-or-cancel orders are enabled in the initial marketplace release.");
        }

        var profile = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Customer profile not found.");

        if (profile.KycStatus != KycStatus.Approved)
        {
            throw new InvalidOperationException("Approved KYC is required before marketplace trading.");
        }

        var pair = await _db.MarketplacePairs
            .Include(x => x.BaseAsset)
            .Include(x => x.QuoteAsset)
            .FirstOrDefaultAsync(x => x.Id == request.MarketplacePairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Marketplace pair not found.");

        ValidatePairAndOrder(pair, request);

        var accounts = await _db.FinancialAccounts
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                (x.AssetCode == pair.BaseAssetCode || x.AssetCode == pair.QuoteAssetCode) &&
                !x.IsDeleted)
            .ToListAsync(ct);

        var baseAccount = accounts.FirstOrDefault(x => x.AssetCode == pair.BaseAssetCode)
            ?? throw new InvalidOperationException($"A {pair.BaseAssetCode} financial account is required to trade this pair.");
        var quoteAccount = accounts.FirstOrDefault(x => x.AssetCode == pair.QuoteAssetCode)
            ?? throw new InvalidOperationException($"A {pair.QuoteAssetCode} financial account is required to trade this pair.");

        EnsureTradeAccount(baseAccount, userId, pair.BaseAssetCode);
        EnsureTradeAccount(quoteAccount, userId, pair.QuoteAssetCode);

        var order = new TradeOrder
        {
            Reference = GenerateReference("KXORD"),
            MarketplacePairId = pair.Id,
            MarketplacePair = pair,
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = userId,
            BaseFinancialAccountId = baseAccount.Id,
            BaseFinancialAccount = baseAccount,
            QuoteFinancialAccountId = quoteAccount.Id,
            QuoteFinancialAccount = quoteAccount,
            Side = request.Side,
            OrderType = request.OrderType,
            TimeInForce = request.TimeInForce,
            Status = TradeOrderStatus.PendingReservation,
            OriginalQuantity = request.Quantity,
            RemainingQuantity = request.Quantity,
            FilledQuantity = 0m,
            LimitPrice = request.LimitPrice,
            ExpiresAt = null,
            CreatedByUserId = userId
        };

        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            _db.TradeOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            var reservationAccount = request.Side == TradeOrderSide.Sell ? baseAccount : quoteAccount;
            var reservationAmount = request.Side == TradeOrderSide.Sell
                ? request.Quantity
                : request.Quantity * request.LimitPrice!.Value;

            var reservation = await _reservationService.ReserveAsync(
                reservationAccount.Id,
                FinancialReservationType.MarketplaceTrade,
                nameof(TradeOrder),
                order.Id,
                reservationAmount,
                userId,
                nameof(MarketplacePair),
                pair.Id,
                ct);

            order.ReservationId = reservation.Id;
            order.Reservation = reservation;
            order.Status = TradeOrderStatus.Open;
            order.OpenedAt = DateTime.UtcNow;
            order.LastUpdatedAt = DateTime.UtcNow;
            order.LastUpdatedByUserId = userId;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await _matchingEngine.MatchOrderAsync(order.Id, ct);

        if (request.TimeInForce == TradeOrderTimeInForce.ImmediateOrCancel)
        {
            var refreshed = await _db.TradeOrders.AsNoTracking().FirstAsync(x => x.Id == order.Id, ct);
            if (refreshed.Status is TradeOrderStatus.Open or TradeOrderStatus.PartiallyFilled)
            {
                await CancelOrderAsync(userId, order.Id, ct);
            }
        }

        return await GetOrderAsync(userId, order.Id, ct);
    }

    public async Task<TradeOrderDto> CancelOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await _db.TradeOrders
            .Include(x => x.MarketplacePair)
            .FirstOrDefaultAsync(x =>
                x.Id == orderId &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Trade order not found.");

        if (order.Status == TradeOrderStatus.Cancelled)
        {
            return ToDto(order);
        }

        if (order.Status is TradeOrderStatus.Filled or TradeOrderStatus.Expired or TradeOrderStatus.Rejected)
        {
            throw new InvalidOperationException($"Order cannot be cancelled while its status is '{order.Status}'.");
        }

        if (order.ReservationId.HasValue)
        {
            var releaseAmount = await CalculateCancellationReleaseAmountAsync(order, ct);
            if (releaseAmount > 0m)
            {
                await _reservationService.ReleaseAsync(
                    order.ReservationId.Value,
                    releaseAmount,
                    $"Marketplace order {order.Reference} cancelled.",
                    userId,
                    ct);
            }
        }

        order.Status = TradeOrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = "Cancelled by user.";
        order.LastUpdatedAt = DateTime.UtcNow;
        order.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(order);
    }

    public async Task<TradeOrderDto> GetOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.TradeOrders
            .AsNoTracking()
            .Include(x => x.MarketplacePair)
            .FirstOrDefaultAsync(x =>
                x.Id == orderId &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Trade order not found.");

        return ToDto(order);
    }

    public async Task<PagedResult<TradeOrderDto>> GetMyOrdersAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TradeOrders
            .AsNoTracking()
            .Include(x => x.MarketplacePair)
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TradeOrderDto>
        {
            Items = items.Select(ToDto).ToList(),
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };
    }


    private async Task<decimal> CalculateCancellationReleaseAmountAsync(TradeOrder order, CancellationToken ct)
    {
        if (!order.ReservationId.HasValue)
        {
            return 0m;
        }

        var reservation = await _db.FinancialReservations
            .AsNoTracking()
            .FirstAsync(x => x.Id == order.ReservationId.Value && !x.IsDeleted, ct);

        var alreadyClosed = reservation.CapturedAmount + reservation.ReleasedAmount;
        var availableToRelease = reservation.Amount - alreadyClosed;
        if (availableToRelease <= 0m)
        {
            return 0m;
        }

        decimal matchedObligation;
        if (order.Side == TradeOrderSide.Sell)
        {
            matchedObligation = await _db.TradeMatches
                .AsNoTracking()
                .Where(x =>
                    x.SellOrderId == order.Id &&
                    (x.Status == TradeMatchStatus.Matched ||
                     x.Status == TradeMatchStatus.SettlementPending) &&
                    !x.IsDeleted)
                .SumAsync(x => (decimal?)x.BaseQuantity, ct) ?? 0m;
        }
        else
        {
            matchedObligation = await _db.TradeMatches
                .AsNoTracking()
                .Where(x =>
                    x.BuyOrderId == order.Id &&
                    (x.Status == TradeMatchStatus.Matched ||
                     x.Status == TradeMatchStatus.SettlementPending) &&
                    !x.IsDeleted)
                .SumAsync(x => (decimal?)x.QuoteQuantity, ct) ?? 0m;
        }

        return Math.Max(0m, availableToRelease - matchedObligation);
    }

    private static void ValidatePairAndOrder(MarketplacePair pair, CreateTradeOrderRequestDto request)
    {
        if (pair.Status != MarketplacePairStatus.Active)
        {
            throw new InvalidOperationException("Marketplace pair is not active.");
        }

        if (!pair.BaseAsset.TradingEnabled || !pair.QuoteAsset.TradingEnabled)
        {
            throw new InvalidOperationException("Trading is disabled for one or more assets in this pair.");
        }

        if (string.Equals(pair.BaseAssetCode, pair.QuoteAssetCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Marketplace pair base and quote assets must be different.");
        }

        if (request.Quantity <= 0m || request.Quantity < pair.MinimumOrderQuantity)
        {
            throw new InvalidOperationException($"Minimum order quantity is {pair.MinimumOrderQuantity} {pair.BaseAssetCode}.");
        }

        if (pair.MaximumOrderQuantity.HasValue && request.Quantity > pair.MaximumOrderQuantity.Value)
        {
            throw new InvalidOperationException($"Maximum order quantity is {pair.MaximumOrderQuantity.Value} {pair.BaseAssetCode}.");
        }

        EnsureIncrement(request.Quantity, pair.QuantityIncrement, "quantity");

        if (!request.LimitPrice.HasValue || request.LimitPrice.Value <= 0m)
        {
            throw new InvalidOperationException("A positive limit price is required.");
        }

        EnsureIncrement(request.LimitPrice.Value, pair.PriceIncrement, "price");

    }

    private static void EnsureIncrement(decimal value, decimal increment, string fieldName)
    {
        if (increment <= 0m)
        {
            throw new InvalidOperationException($"Marketplace pair {fieldName} increment is invalid.");
        }

        if (value % increment != 0m)
        {
            throw new InvalidOperationException($"Order {fieldName} must be a multiple of {increment}.");
        }
    }

    private static void EnsureTradeAccount(FinancialAccount account, Guid userId, string expectedAssetCode)
    {
        if (account.OwnerType != FinancialAccountOwnerType.User || account.OwnerId != userId)
        {
            throw new InvalidOperationException("Financial account ownership does not match the authenticated user.");
        }

        if (!string.Equals(account.AssetCode, expectedAssetCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Financial account asset does not match the marketplace pair.");
        }

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new InvalidOperationException($"The {account.AssetCode} financial account is not active.");
        }
    }

    private static TradeOrderDto ToDto(TradeOrder x) => new(
        x.Id,
        x.Reference,
        x.MarketplacePairId,
        x.MarketplacePair.Code,
        x.Side,
        x.OrderType,
        x.TimeInForce,
        x.Status,
        x.OriginalQuantity,
        x.RemainingQuantity,
        x.FilledQuantity,
        x.LimitPrice,
        x.AverageFillPrice,
        x.CreatedAt,
        x.OpenedAt,
        x.ExpiresAt);

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];
}
