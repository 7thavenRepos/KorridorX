using KorridorX.Data;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Marketplace;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Marketplace;

public interface IMarketplaceOperationsService
{
    Task<MarketplacePairDto> CreatePairAsync(CreateMarketplacePairRequestDto request, Guid? actionedByUserId, CancellationToken ct = default);
    Task<MarketplacePairDto> UpdatePairAsync(Guid pairId, UpdateMarketplacePairRequestDto request, Guid? actionedByUserId, CancellationToken ct = default);
    Task<MarketplacePairDto> SetPairStatusAsync(Guid pairId, MarketplacePairStatus status, Guid? actionedByUserId, CancellationToken ct = default);

    Task<PagedResult<TradeHistoryDto>> GetMyTradesAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PagedResult<TradeOrderDto>> GetOrdersAsync(Guid? pairId, TradeOrderStatus? status, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<PagedResult<TradeDto>> GetTradesAsync(Guid? pairId, TradeStatus? status, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<PagedResult<TradeMatchDto>> GetMatchesAsync(Guid? pairId, TradeMatchStatus? status, int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<MarketplaceMaintenanceResultDto> ExpireOrdersAsync(int take = 200, Guid? actionedByUserId = null, CancellationToken ct = default);
    Task<MarketplaceRecoveryResultDto> RecoverSettlementsAsync(int take = 100, CancellationToken ct = default);
}

public class MarketplaceOperationsService : IMarketplaceOperationsService
{
    private readonly AppDbContext _db;
    private readonly IFinancialReservationService _reservationService;
    private readonly IMarketplaceSettlementService _settlementService;

    public MarketplaceOperationsService(
        AppDbContext db,
        IFinancialReservationService reservationService,
        IMarketplaceSettlementService settlementService)
    {
        _db = db;
        _reservationService = reservationService;
        _settlementService = settlementService;
    }

    public async Task<MarketplacePairDto> CreatePairAsync(
        CreateMarketplacePairRequestDto request,
        Guid? actionedByUserId,
        CancellationToken ct = default)
    {
        var baseCode = NormalizeAssetCode(request.BaseAssetCode);
        var quoteCode = NormalizeAssetCode(request.QuoteAssetCode);

        if (baseCode == quoteCode)
        {
            throw new InvalidOperationException("Marketplace base and quote assets must be different.");
        }

        ValidatePairLimits(
            request.MinimumOrderQuantity,
            request.MaximumOrderQuantity,
            request.QuantityIncrement,
            request.PriceIncrement);

        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.Code == baseCode || x.Code == quoteCode)
            .ToListAsync(ct);

        if (assets.Count != 2)
        {
            throw new InvalidOperationException("One or more marketplace assets do not exist.");
        }

        if (await _db.MarketplacePairs.AnyAsync(x =>
                !x.IsDeleted &&
                (x.Code == $"{baseCode}/{quoteCode}" ||
                 (x.BaseAssetCode == baseCode && x.QuoteAssetCode == quoteCode)), ct))
        {
            throw new InvalidOperationException("Marketplace pair already exists.");
        }

        var pair = new MarketplacePair
        {
            Code = $"{baseCode}/{quoteCode}",
            BaseAssetCode = baseCode,
            QuoteAssetCode = quoteCode,
            Status = request.Status,
            MinimumOrderQuantity = request.MinimumOrderQuantity,
            MaximumOrderQuantity = request.MaximumOrderQuantity,
            QuantityIncrement = request.QuantityIncrement,
            PriceIncrement = request.PriceIncrement,
            CreatedByUserId = actionedByUserId
        };

        _db.MarketplacePairs.Add(pair);
        await _db.SaveChangesAsync(ct);

        return ToPairDto(pair);
    }

    public async Task<MarketplacePairDto> UpdatePairAsync(
        Guid pairId,
        UpdateMarketplacePairRequestDto request,
        Guid? actionedByUserId,
        CancellationToken ct = default)
    {
        ValidatePairLimits(
            request.MinimumOrderQuantity,
            request.MaximumOrderQuantity,
            request.QuantityIncrement,
            request.PriceIncrement);

        var pair = await _db.MarketplacePairs
            .FirstOrDefaultAsync(x => x.Id == pairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Marketplace pair not found.");

        pair.MinimumOrderQuantity = request.MinimumOrderQuantity;
        pair.MaximumOrderQuantity = request.MaximumOrderQuantity;
        pair.QuantityIncrement = request.QuantityIncrement;
        pair.PriceIncrement = request.PriceIncrement;
        pair.LastUpdatedAt = DateTime.UtcNow;
        pair.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);
        return ToPairDto(pair);
    }

    public async Task<MarketplacePairDto> SetPairStatusAsync(
        Guid pairId,
        MarketplacePairStatus status,
        Guid? actionedByUserId,
        CancellationToken ct = default)
    {
        var pair = await _db.MarketplacePairs
            .FirstOrDefaultAsync(x => x.Id == pairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Marketplace pair not found.");

        pair.Status = status;
        pair.LastUpdatedAt = DateTime.UtcNow;
        pair.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);
        return ToPairDto(pair);
    }

    public async Task<PagedResult<TradeHistoryDto>> GetMyTradesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Trades
            .AsNoTracking()
            .Include(x => x.MarketplacePair)
            .Include(x => x.BuyerBaseFinancialAccount)
            .Include(x => x.SellerBaseFinancialAccount)
            .Where(x =>
                !x.IsDeleted &&
                ((x.BuyerBaseFinancialAccount.OwnerType == FinancialAccountOwnerType.User &&
                  x.BuyerBaseFinancialAccount.OwnerId == userId) ||
                 (x.SellerBaseFinancialAccount.OwnerType == FinancialAccountOwnerType.User &&
                  x.SellerBaseFinancialAccount.OwnerId == userId)))
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            var isBuyer =
                x.BuyerBaseFinancialAccount.OwnerType == FinancialAccountOwnerType.User &&
                x.BuyerBaseFinancialAccount.OwnerId == userId;

            return new TradeHistoryDto(
                x.Id,
                x.Reference,
                x.TradeMatchId,
                x.MarketplacePairId,
                x.MarketplacePair.Code,
                isBuyer ? TradeOrderSide.Buy : TradeOrderSide.Sell,
                x.Price,
                x.BaseQuantity,
                x.QuoteQuantity,
                x.Status,
                x.CreatedAt,
                x.CompletedAt);
        }).ToList();

        return Page(items, total, page, pageSize);
    }

    public async Task<PagedResult<TradeOrderDto>> GetOrdersAsync(
        Guid? pairId,
        TradeOrderStatus? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.TradeOrders
            .AsNoTracking()
            .Include(x => x.MarketplacePair)
            .Where(x => !x.IsDeleted);

        if (pairId.HasValue)
        {
            query = query.Where(x => x.MarketplacePairId == pairId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page(rows.Select(ToOrderDto).ToList(), total, page, pageSize);
    }

    public async Task<PagedResult<TradeDto>> GetTradesAsync(
        Guid? pairId,
        TradeStatus? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Trades
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (pairId.HasValue)
        {
            query = query.Where(x => x.MarketplacePairId == pairId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TradeDto(
                x.Id,
                x.Reference,
                x.TradeMatchId,
                x.MarketplacePairId,
                x.Price,
                x.BaseQuantity,
                x.QuoteQuantity,
                x.Status,
                x.CreatedAt,
                x.CompletedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    public async Task<PagedResult<TradeMatchDto>> GetMatchesAsync(
        Guid? pairId,
        TradeMatchStatus? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.TradeMatches
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (pairId.HasValue)
        {
            query = query.Where(x => x.MarketplacePairId == pairId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.MatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TradeMatchDto(
                x.Id,
                x.Reference,
                x.BuyOrderId,
                x.SellOrderId,
                x.Price,
                x.BaseQuantity,
                x.QuoteQuantity,
                x.Status,
                x.MatchedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    public async Task<MarketplaceMaintenanceResultDto> ExpireOrdersAsync(
        int take = 200,
        Guid? actionedByUserId = null,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 1000);
        var now = DateTime.UtcNow;

        var orderIds = await _db.TradeOrders
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.ExpiresAt.HasValue &&
                x.ExpiresAt <= now &&
                (x.Status == TradeOrderStatus.Open ||
                 x.Status == TradeOrderStatus.PartiallyFilled))
            .OrderBy(x => x.ExpiresAt)
            .Select(x => x.Id)
            .Take(take)
            .ToListAsync(ct);

        var expired = 0;
        var released = 0m;
        var failures = new List<MarketplaceOperationFailureDto>();

        foreach (var orderId in orderIds)
        {
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                var order = await _db.TradeOrders
                    .FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, ct);

                if (order is null ||
                    order.Status is not TradeOrderStatus.Open and not TradeOrderStatus.PartiallyFilled ||
                    !order.ExpiresAt.HasValue ||
                    order.ExpiresAt > DateTime.UtcNow)
                {
                    await tx.RollbackAsync(ct);
                    continue;
                }

                var releaseAmount = await CalculateReleasableAmountAsync(order, ct);

                if (order.ReservationId.HasValue && releaseAmount > 0m)
                {
                    await _reservationService.ReleaseAsync(
                        order.ReservationId.Value,
                        releaseAmount,
                        $"Marketplace order {order.Reference} expired.",
                        actionedByUserId,
                        ct);

                    released += releaseAmount;
                }

                order.Status = TradeOrderStatus.Expired;
                order.LastUpdatedAt = DateTime.UtcNow;
                order.LastUpdatedByUserId = actionedByUserId;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                expired++;
                _db.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                failures.Add(new MarketplaceOperationFailureDto(orderId, ex.Message));
            }
        }

        return new MarketplaceMaintenanceResultDto(orderIds.Count, expired, released, failures);
    }

    public async Task<MarketplaceRecoveryResultDto> RecoverSettlementsAsync(
        int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 1000);

        var matchIds = await _db.TradeMatches
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == TradeMatchStatus.Matched ||
                 x.Status == TradeMatchStatus.SettlementPending))
            .OrderBy(x => x.MatchedAt)
            .Select(x => x.Id)
            .Take(take)
            .ToListAsync(ct);

        var recovered = 0;
        var failures = new List<MarketplaceOperationFailureDto>();

        foreach (var matchId in matchIds)
        {
            try
            {
                _db.ChangeTracker.Clear();
                await _settlementService.SettleMatchAsync(matchId, ct);
                recovered++;
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                failures.Add(new MarketplaceOperationFailureDto(matchId, ex.Message));
            }
        }

        return new MarketplaceRecoveryResultDto(matchIds.Count, recovered, failures);
    }

    private async Task<decimal> CalculateReleasableAmountAsync(TradeOrder order, CancellationToken ct)
    {
        if (!order.ReservationId.HasValue)
        {
            return 0m;
        }

        var reservation = await _db.FinancialReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.ReservationId.Value && !x.IsDeleted, ct);

        if (reservation is null || reservation.Status != FinancialReservationStatus.Active)
        {
            return 0m;
        }

        var remainingReservation =
            reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount;

        if (remainingReservation <= 0m)
        {
            return 0m;
        }

        decimal unsettledObligation;

        if (order.Side == TradeOrderSide.Sell)
        {
            unsettledObligation = await _db.TradeMatches
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
            unsettledObligation = await _db.TradeMatches
                .AsNoTracking()
                .Where(x =>
                    x.BuyOrderId == order.Id &&
                    (x.Status == TradeMatchStatus.Matched ||
                     x.Status == TradeMatchStatus.SettlementPending) &&
                    !x.IsDeleted)
                .SumAsync(x => (decimal?)x.QuoteQuantity, ct) ?? 0m;
        }

        return Math.Max(0m, remainingReservation - unsettledObligation);
    }

    private static void ValidatePairLimits(
        decimal minimumOrderQuantity,
        decimal? maximumOrderQuantity,
        decimal quantityIncrement,
        decimal priceIncrement)
    {
        if (minimumOrderQuantity <= 0m)
        {
            throw new InvalidOperationException("Minimum order quantity must be greater than zero.");
        }

        if (maximumOrderQuantity.HasValue && maximumOrderQuantity.Value < minimumOrderQuantity)
        {
            throw new InvalidOperationException("Maximum order quantity cannot be less than the minimum.");
        }

        if (quantityIncrement <= 0m || priceIncrement <= 0m)
        {
            throw new InvalidOperationException("Marketplace quantity and price increments must be greater than zero.");
        }

        if (minimumOrderQuantity % quantityIncrement != 0m)
        {
            throw new InvalidOperationException("Minimum order quantity must align with the quantity increment.");
        }

        if (maximumOrderQuantity.HasValue && maximumOrderQuantity.Value % quantityIncrement != 0m)
        {
            throw new InvalidOperationException("Maximum order quantity must align with the quantity increment.");
        }
    }

    private static string NormalizeAssetCode(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20)
        {
            throw new InvalidOperationException("Invalid marketplace asset code.");
        }

        return normalized;
    }

    private static MarketplacePairDto ToPairDto(MarketplacePair x) => new(
        x.Id,
        x.Code,
        x.BaseAssetCode,
        x.QuoteAssetCode,
        x.Status,
        x.MinimumOrderQuantity,
        x.MaximumOrderQuantity,
        x.QuantityIncrement,
        x.PriceIncrement);

    private static TradeOrderDto ToOrderDto(TradeOrder x) => new(
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

    private static PagedResult<T> Page<T>(
        IReadOnlyList<T> items,
        int total,
        int page,
        int pageSize) =>
        new()
        {
            Items = items.ToList(),
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };
}
