using System.Data;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Marketplace;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Marketplace;

public class MarketplaceMatchingEngine : IMarketplaceMatchingEngine
{
    private readonly AppDbContext _db;
    private readonly IMarketplaceSettlementService _settlementService;

    public MarketplaceMatchingEngine(
        AppDbContext db,
        IMarketplaceSettlementService settlementService)
    {
        _db = db;
        _settlementService = settlementService;
    }

    public async Task<int> MatchOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var matchesCreated = 0;

        while (true)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var incoming = await _db.TradeOrders
                .Include(x => x.MarketplacePair)
                .FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Trade order not found.");

            if (incoming.MarketplacePair.Status != MarketplacePairStatus.Active ||
                !IsMatchable(incoming))
            {
                await tx.RollbackAsync(ct);
                return matchesCreated;
            }

            var candidate = await FindBestCandidateAsync(incoming, ct);
            if (candidate is null)
            {
                await tx.RollbackAsync(ct);
                return matchesCreated;
            }

            if (candidate.OwnerType == incoming.OwnerType && candidate.OwnerId == incoming.OwnerId)
            {
                // Skip self-match candidate and search again excluding it.
                candidate = await FindBestCandidateAsync(incoming, ct, candidate.Id);
                if (candidate is null)
                {
                    await tx.RollbackAsync(ct);
                    return matchesCreated;
                }
            }

            var buyOrder = incoming.Side == TradeOrderSide.Buy ? incoming : candidate;
            var sellOrder = incoming.Side == TradeOrderSide.Sell ? incoming : candidate;

            if (!PricesCross(buyOrder, sellOrder))
            {
                await tx.RollbackAsync(ct);
                return matchesCreated;
            }

            var executionPrice = candidate.LimitPrice
                ?? throw new InvalidOperationException("Resting marketplace order has no limit price.");
            var baseQuantity = Math.Min(incoming.RemainingQuantity, candidate.RemainingQuantity);
            var quoteQuantity = baseQuantity * executionPrice;
            var now = DateTime.UtcNow;

            var match = new TradeMatch
            {
                Reference = GenerateReference("KXMAT"),
                MarketplacePairId = incoming.MarketplacePairId,
                MarketplacePair = incoming.MarketplacePair,
                BuyOrderId = buyOrder.Id,
                BuyOrder = buyOrder,
                SellOrderId = sellOrder.Id,
                SellOrder = sellOrder,
                MakerOrderId = candidate.Id,
                TakerOrderId = incoming.Id,
                Price = executionPrice,
                BaseQuantity = baseQuantity,
                QuoteQuantity = quoteQuantity,
                Status = TradeMatchStatus.Matched,
                MatchedAt = now
            };

            ApplyFill(incoming, baseQuantity, executionPrice, now);
            ApplyFill(candidate, baseQuantity, executionPrice, now);
            _db.TradeMatches.Add(match);

            try
            {
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var matchId = match.Id;
                _db.ChangeTracker.Clear();
                await _settlementService.SettleMatchAsync(matchId, ct);
                _db.ChangeTracker.Clear();

                matchesCreated++;
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                continue;
            }
        }
    }

    private async Task<TradeOrder?> FindBestCandidateAsync(
        TradeOrder incoming,
        CancellationToken ct,
        Guid? excludedOrderId = null)
    {
        var oppositeSide = incoming.Side == TradeOrderSide.Buy ? TradeOrderSide.Sell : TradeOrderSide.Buy;
        var activeStatuses = new[] { TradeOrderStatus.Open, TradeOrderStatus.PartiallyFilled };
        var now = DateTime.UtcNow;

        var query = _db.TradeOrders
            .Where(x =>
                x.MarketplacePairId == incoming.MarketplacePairId &&
                x.Id != incoming.Id &&
                (!excludedOrderId.HasValue || x.Id != excludedOrderId.Value) &&
                x.Side == oppositeSide &&
                activeStatuses.Contains(x.Status) &&
                x.RemainingQuantity > 0m &&
                (!x.ExpiresAt.HasValue || x.ExpiresAt > now) &&
                x.LimitPrice != null &&
                !x.IsDeleted &&
                !(x.OwnerType == incoming.OwnerType && x.OwnerId == incoming.OwnerId));

        if (incoming.Side == TradeOrderSide.Buy)
        {
            query = query.Where(x => x.LimitPrice <= incoming.LimitPrice);
            return await query
                .OrderBy(x => x.LimitPrice)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
        }

        query = query.Where(x => x.LimitPrice >= incoming.LimitPrice);
        return await query
            .OrderByDescending(x => x.LimitPrice)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static bool IsMatchable(TradeOrder order) =>
        (order.Status is TradeOrderStatus.Open or TradeOrderStatus.PartiallyFilled) &&
        order.RemainingQuantity > 0m &&
        order.LimitPrice.HasValue;

    private static bool PricesCross(TradeOrder buyOrder, TradeOrder sellOrder) =>
        buyOrder.LimitPrice.HasValue &&
        sellOrder.LimitPrice.HasValue &&
        buyOrder.LimitPrice.Value >= sellOrder.LimitPrice.Value;

    private static void ApplyFill(TradeOrder order, decimal quantity, decimal price, DateTime now)
    {
        var previousFilled = order.FilledQuantity;
        var newFilled = previousFilled + quantity;
        var weightedValue = (order.AverageFillPrice ?? 0m) * previousFilled + price * quantity;

        order.FilledQuantity = newFilled;
        order.RemainingQuantity -= quantity;
        order.AverageFillPrice = newFilled > 0m ? weightedValue / newFilled : null;
        order.LastUpdatedAt = now;

        if (order.RemainingQuantity <= 0m)
        {
            order.RemainingQuantity = 0m;
            order.Status = TradeOrderStatus.Filled;
            order.FilledAt = now;
        }
        else
        {
            order.Status = TradeOrderStatus.PartiallyFilled;
        }
    }

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];
}
