using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Pagination;

public sealed class BusinessOrderBookPriceLevel
{
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public int OrderCount { get; set; }
}

public static class BusinessOrderBookPageQueries
{
    public static IOrderedQueryable<BusinessOrderBookPriceLevel> Levels(
        AppDbContext db, Guid pairId, TradeOrderSide side, DateTime now)
    {
        if (side is not TradeOrderSide.Buy and not TradeOrderSide.Sell)
            throw new ArgumentOutOfRangeException(nameof(side));
        // Aggregate in SQL before counting or paging; no fixed-depth window.
        var levels = db.TradeOrders.AsNoTracking()
            .Where(x => x.MarketplacePairId == pairId && x.Side == side && !x.IsDeleted &&
                (x.Status == TradeOrderStatus.Open || x.Status == TradeOrderStatus.PartiallyFilled) &&
                x.RemainingQuantity > 0m && x.LimitPrice != null &&
                (!x.ExpiresAt.HasValue || x.ExpiresAt > now))
            .GroupBy(x => x.LimitPrice!.Value)
            .Select(g => new BusinessOrderBookPriceLevel
            {
                Price = g.Key, Quantity = g.Sum(x => x.RemainingQuantity), OrderCount = g.Count()
            });
        return side == TradeOrderSide.Buy ? levels.OrderByDescending(x => x.Price) : levels.OrderBy(x => x.Price);
    }
}
