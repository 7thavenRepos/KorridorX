using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Marketplace;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Marketplace;

public sealed class BusinessTradingRfqService : IBusinessTradingRfqService
{
    private readonly AppDbContext _db;
    private readonly IFinancialReservationService _reservations;
    private readonly IMarketplaceSettlementService _settlement;

    public BusinessTradingRfqService(AppDbContext db, IFinancialReservationService reservations, IMarketplaceSettlementService settlement)
    {
        _db = db;
        _reservations = reservations;
        _settlement = settlement;
    }

    public async Task<BusinessTradingRfqDto> CreateAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, FinancialAccountOwnerType counterpartyOwnerType, Guid counterpartyOwnerId, Guid? actionedByUserId, Guid marketplacePairId, TradeOrderSide side, decimal quantity, DateTime? expiresAt, CancellationToken ct = default)
    {
        if (requesterOwnerType == counterpartyOwnerType && requesterOwnerId == counterpartyOwnerId)
            throw new InvalidOperationException("An RFQ counterparty must be different from the requester.");

        var pair = await GetActivePairAsync(marketplacePairId, ct);
        ValidateQuantity(pair, quantity);
        var expiry = expiresAt ?? DateTime.UtcNow.AddMinutes(10);
        if (expiry <= DateTime.UtcNow) throw new InvalidOperationException("RFQ expiry must be in the future.");

        await EnsureOwnerAccountsAsync(requesterOwnerType, requesterOwnerId, pair, ct);
        await EnsureOwnerAccountsAsync(counterpartyOwnerType, counterpartyOwnerId, pair, ct);

        var rfq = new BusinessTradingRfq
        {
            Reference = GenerateReference("KXRFQ"),
            MarketplacePairId = pair.Id,
            MarketplacePair = pair,
            RequesterOwnerType = requesterOwnerType,
            RequesterOwnerId = requesterOwnerId,
            CounterpartyOwnerType = counterpartyOwnerType,
            CounterpartyOwnerId = counterpartyOwnerId,
            Side = side,
            Quantity = quantity,
            Status = BusinessTradingRfqStatus.Open,
            ExpiresAt = expiry,
            CreatedByUserId = actionedByUserId
        };

        _db.BusinessTradingRfqs.Add(rfq);
        await _db.SaveChangesAsync(ct);
        return ToDto(rfq);
    }

    public async Task<BusinessTradingRfqDto> QuoteAsync(FinancialAccountOwnerType responderOwnerType, Guid responderOwnerId, Guid? actionedByUserId, Guid rfqId, decimal price, DateTime? expiresAt, CancellationToken ct = default)
    {
        if (price <= 0m) throw new InvalidOperationException("RFQ quote price must be greater than zero.");
        var rfq = await GetTrackedRfqAsync(rfqId, ct);
        if (ExpireIfNeeded(rfq))
            await _db.SaveChangesAsync(ct);

        if (rfq.Status is BusinessTradingRfqStatus.Accepted or BusinessTradingRfqStatus.Rejected or BusinessTradingRfqStatus.Cancelled or BusinessTradingRfqStatus.Expired)
            throw new InvalidOperationException($"RFQ cannot be quoted while its status is '{rfq.Status}'.");
        if (rfq.CounterpartyOwnerType != responderOwnerType || rfq.CounterpartyOwnerId != responderOwnerId)
            throw new UnauthorizedAccessException("Only the selected RFQ counterparty can submit a quote.");

        ValidatePrice(rfq.MarketplacePair, price);
        var expiry = expiresAt ?? DateTime.UtcNow.AddMinutes(5);
        if (expiry <= DateTime.UtcNow) throw new InvalidOperationException("RFQ quote expiry must be in the future.");
        if (expiry > rfq.ExpiresAt) expiry = rfq.ExpiresAt;

        var now = DateTime.UtcNow;
        foreach (var active in rfq.Quotes.Where(x => x.Status == BusinessTradingRfqQuoteStatus.Active && !x.IsDeleted))
        {
            active.Status = BusinessTradingRfqQuoteStatus.Superseded;
            active.SupersededAt = now;
            active.LastUpdatedAt = now;
            active.LastUpdatedByUserId = actionedByUserId;
        }

        var quote = new BusinessTradingRfqQuote
        {
            Reference = GenerateReference("KXRFQQ"),
            BusinessTradingRfqId = rfq.Id,
            BusinessTradingRfq = rfq,
            ResponderOwnerType = responderOwnerType,
            ResponderOwnerId = responderOwnerId,
            Price = price,
            Status = BusinessTradingRfqQuoteStatus.Active,
            ExpiresAt = expiry,
            CreatedByUserId = actionedByUserId
        };

        _db.BusinessTradingRfqQuotes.Add(quote);
        rfq.Status = BusinessTradingRfqStatus.Quoted;
        rfq.LastUpdatedAt = now;
        rfq.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);

        return ToDto(rfq);
    }

    public async Task<BusinessTradingRfqDto> AcceptAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, Guid? actionedByUserId, Guid rfqId, Guid quoteId, CancellationToken ct = default)
    {
        var rfq = await GetTrackedRfqAsync(rfqId, ct);
        if (ExpireIfNeeded(rfq))
            await _db.SaveChangesAsync(ct);

        if (rfq.RequesterOwnerType != requesterOwnerType || rfq.RequesterOwnerId != requesterOwnerId)
            throw new UnauthorizedAccessException("Only the RFQ requester can accept a quote.");

        if (rfq.Status == BusinessTradingRfqStatus.Accepted)
        {
            if (rfq.AcceptedQuoteId == quoteId && rfq.TradeMatchId.HasValue)
            {
                await _settlement.SettleMatchAsync(rfq.TradeMatchId.Value, ct);
                return await GetAsync(requesterOwnerType, requesterOwnerId, rfq.Id, ct);
            }
            throw new InvalidOperationException("RFQ has already been accepted.");
        }

        if (rfq.Status is BusinessTradingRfqStatus.Rejected or BusinessTradingRfqStatus.Cancelled or BusinessTradingRfqStatus.Expired)
            throw new InvalidOperationException($"RFQ cannot be accepted while its status is '{rfq.Status}'.");

        var quote = rfq.Quotes.SingleOrDefault(x => x.Id == quoteId && !x.IsDeleted) ?? throw new InvalidOperationException("RFQ quote not found.");
        if (quote.Status != BusinessTradingRfqQuoteStatus.Active) throw new InvalidOperationException("Only an active RFQ quote can be accepted.");
        if (quote.ExpiresAt <= DateTime.UtcNow)
        {
            quote.Status = BusinessTradingRfqQuoteStatus.Expired;
            quote.ExpiredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("RFQ quote has expired.");
        }

        var pair = rfq.MarketplacePair;
        ValidateQuantity(pair, rfq.Quantity);
        ValidatePrice(pair, quote.Price);
        var requesterAccounts = await GetOwnerAccountsAsync(rfq.RequesterOwnerType, rfq.RequesterOwnerId, pair, ct);
        var counterpartyAccounts = await GetOwnerAccountsAsync(rfq.CounterpartyOwnerType, rfq.CounterpartyOwnerId, pair, ct);
        var requesterIsBuyer = rfq.Side == TradeOrderSide.Buy;
        var buyerOwnerType = requesterIsBuyer ? rfq.RequesterOwnerType : rfq.CounterpartyOwnerType;
        var buyerOwnerId = requesterIsBuyer ? rfq.RequesterOwnerId : rfq.CounterpartyOwnerId;
        var buyerAccounts = requesterIsBuyer ? requesterAccounts : counterpartyAccounts;
        var sellerOwnerType = requesterIsBuyer ? rfq.CounterpartyOwnerType : rfq.RequesterOwnerType;
        var sellerOwnerId = requesterIsBuyer ? rfq.CounterpartyOwnerId : rfq.RequesterOwnerId;
        var sellerAccounts = requesterIsBuyer ? counterpartyAccounts : requesterAccounts;
        var quoteQuantity = rfq.Quantity * quote.Price;
        if (quoteQuantity <= 0m) throw new InvalidOperationException("RFQ quote amount is invalid.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow;
        var buyOrder = NewFilledOrder(pair, buyerOwnerType, buyerOwnerId, buyerAccounts.Base.Id, buyerAccounts.Quote.Id, TradeOrderSide.Buy, rfq.Quantity, quote.Price, now, actionedByUserId);
        var sellOrder = NewFilledOrder(pair, sellerOwnerType, sellerOwnerId, sellerAccounts.Base.Id, sellerAccounts.Quote.Id, TradeOrderSide.Sell, rfq.Quantity, quote.Price, now, actionedByUserId);
        _db.TradeOrders.AddRange(buyOrder, sellOrder);
        await _db.SaveChangesAsync(ct);

        var sellReservation = await _reservations.ReserveAsync(sellerAccounts.Base.Id, FinancialReservationType.MarketplaceTrade, nameof(TradeOrder), sellOrder.Id, rfq.Quantity, actionedByUserId, nameof(BusinessTradingRfq), rfq.Id, ct);
        var buyReservation = await _reservations.ReserveAsync(buyerAccounts.Quote.Id, FinancialReservationType.MarketplaceTrade, nameof(TradeOrder), buyOrder.Id, quoteQuantity, actionedByUserId, nameof(BusinessTradingRfq), rfq.Id, ct);
        sellOrder.ReservationId = sellReservation.Id;
        buyOrder.ReservationId = buyReservation.Id;

        var match = new TradeMatch
        {
            Reference = GenerateReference("KXMAT"), MarketplacePairId = pair.Id, MarketplacePair = pair,
            BuyOrderId = buyOrder.Id, BuyOrder = buyOrder, SellOrderId = sellOrder.Id, SellOrder = sellOrder,
            MakerOrderId = sellOrder.Id, TakerOrderId = buyOrder.Id, Price = quote.Price,
            BaseQuantity = rfq.Quantity, QuoteQuantity = quoteQuantity, Status = TradeMatchStatus.Matched,
            MatchedAt = now, CreatedByUserId = actionedByUserId
        };
        _db.TradeMatches.Add(match);

        quote.Status = BusinessTradingRfqQuoteStatus.Accepted;
        quote.AcceptedAt = now;
        quote.LastUpdatedAt = now;
        quote.LastUpdatedByUserId = actionedByUserId;
        foreach (var other in rfq.Quotes.Where(x => x.Id != quote.Id && x.Status == BusinessTradingRfqQuoteStatus.Active && !x.IsDeleted))
        {
            other.Status = BusinessTradingRfqQuoteStatus.Rejected;
            other.RejectedAt = now;
            other.LastUpdatedAt = now;
            other.LastUpdatedByUserId = actionedByUserId;
        }

        rfq.AcceptedQuoteId = quote.Id;
        rfq.TradeMatchId = match.Id;
        rfq.Status = BusinessTradingRfqStatus.Accepted;
        rfq.AcceptedAt = now;
        rfq.LastUpdatedAt = now;
        rfq.LastUpdatedByUserId = actionedByUserId;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _db.ChangeTracker.Clear();
        await _settlement.SettleMatchAsync(match.Id, ct);
        _db.ChangeTracker.Clear();
        return await GetAsync(requesterOwnerType, requesterOwnerId, rfq.Id, ct);
    }

    public async Task<BusinessTradingRfqDto> CancelAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, Guid? actionedByUserId, Guid rfqId, CancellationToken ct = default)
    {
        var rfq = await GetTrackedRfqAsync(rfqId, ct);
        if (ExpireIfNeeded(rfq))
            await _db.SaveChangesAsync(ct);
        if (rfq.RequesterOwnerType != requesterOwnerType || rfq.RequesterOwnerId != requesterOwnerId)
            throw new UnauthorizedAccessException("Only the RFQ requester can cancel the RFQ.");
        if (rfq.Status == BusinessTradingRfqStatus.Cancelled) return ToDto(rfq);
        if (rfq.Status is BusinessTradingRfqStatus.Accepted or BusinessTradingRfqStatus.Rejected or BusinessTradingRfqStatus.Expired)
            throw new InvalidOperationException($"RFQ cannot be cancelled while its status is '{rfq.Status}'.");
        var now = DateTime.UtcNow;
        rfq.Status = BusinessTradingRfqStatus.Cancelled;
        rfq.CancelledAt = now;
        rfq.LastUpdatedAt = now;
        rfq.LastUpdatedByUserId = actionedByUserId;
        foreach (var active in rfq.Quotes.Where(x => x.Status == BusinessTradingRfqQuoteStatus.Active && !x.IsDeleted))
        {
            active.Status = BusinessTradingRfqQuoteStatus.Rejected;
            active.RejectedAt = now;
            active.LastUpdatedAt = now;
            active.LastUpdatedByUserId = actionedByUserId;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(rfq);
    }

    public async Task<BusinessTradingRfqDto> GetAsync(FinancialAccountOwnerType ownerType, Guid ownerId, Guid rfqId, CancellationToken ct = default)
    {
        var rfq = await _db.BusinessTradingRfqs.AsNoTracking().Include(x => x.MarketplacePair).Include(x => x.Quotes)
            .FirstOrDefaultAsync(x => x.Id == rfqId && !x.IsDeleted && ((x.RequesterOwnerType == ownerType && x.RequesterOwnerId == ownerId) || (x.CounterpartyOwnerType == ownerType && x.CounterpartyOwnerId == ownerId)), ct)
            ?? throw new InvalidOperationException("RFQ not found.");
        return ToDto(rfq);
    }

    public async Task<PagedResult<BusinessTradingRfqDto>> GetForOwnerAsync(FinancialAccountOwnerType ownerType, Guid ownerId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.BusinessTradingRfqs.AsNoTracking().Include(x => x.MarketplacePair).Include(x => x.Quotes)
            .Where(x => !x.IsDeleted && ((x.RequesterOwnerType == ownerType && x.RequesterOwnerId == ownerId) || (x.CounterpartyOwnerType == ownerType && x.CounterpartyOwnerId == ownerId)))
            .OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<BusinessTradingRfqDto> { Items = rows.Select(ToDto).ToList(), Meta = new PageMeta { Page = page, PageSize = pageSize, TotalItems = total } };
    }

    private async Task<BusinessTradingRfq> GetTrackedRfqAsync(Guid rfqId, CancellationToken ct) =>
        await _db.BusinessTradingRfqs.Include(x => x.MarketplacePair).Include(x => x.Quotes).FirstOrDefaultAsync(x => x.Id == rfqId && !x.IsDeleted, ct)
        ?? throw new InvalidOperationException("RFQ not found.");

    private static bool ExpireIfNeeded(BusinessTradingRfq rfq)
    {
        if (rfq.ExpiresAt > DateTime.UtcNow ||
            rfq.Status is not (BusinessTradingRfqStatus.Open or BusinessTradingRfqStatus.Quoted))
            return false;

        var now = DateTime.UtcNow;

        rfq.Status = BusinessTradingRfqStatus.Expired;
        rfq.ExpiredAt = now;
        rfq.LastUpdatedAt = now;

        foreach (var quote in rfq.Quotes.Where(x =>
                     x.Status == BusinessTradingRfqQuoteStatus.Active &&
                     !x.IsDeleted))
        {
            quote.Status = BusinessTradingRfqQuoteStatus.Expired;
            quote.ExpiredAt = now;
            quote.LastUpdatedAt = now;
        }

        return true;
    }

    private async Task<MarketplacePair> GetActivePairAsync(Guid pairId, CancellationToken ct) =>
        await _db.MarketplacePairs.Include(x => x.BaseAsset).Include(x => x.QuoteAsset).FirstOrDefaultAsync(x => x.Id == pairId && x.Status == MarketplacePairStatus.Active && !x.IsDeleted, ct)
        ?? throw new InvalidOperationException("Active marketplace pair not found.");

    private async Task EnsureOwnerAccountsAsync(FinancialAccountOwnerType ownerType, Guid ownerId, MarketplacePair pair, CancellationToken ct) => _ = await GetOwnerAccountsAsync(ownerType, ownerId, pair, ct);

    private async Task<OwnerAccounts> GetOwnerAccountsAsync(FinancialAccountOwnerType ownerType, Guid ownerId, MarketplacePair pair, CancellationToken ct)
    {
        var accounts = await _db.FinancialAccounts.Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.AccountType == FinancialAccountType.Customer && (x.AssetCode == pair.BaseAssetCode || x.AssetCode == pair.QuoteAssetCode) && !x.IsDeleted).ToListAsync(ct);
        var baseAccount = accounts.SingleOrDefault(x => x.AssetCode == pair.BaseAssetCode) ?? throw new InvalidOperationException($"RFQ participant requires a {pair.BaseAssetCode} financial account.");
        var quoteAccount = accounts.SingleOrDefault(x => x.AssetCode == pair.QuoteAssetCode) ?? throw new InvalidOperationException($"RFQ participant requires a {pair.QuoteAssetCode} financial account.");
        ValidateAccount(baseAccount, ownerType, ownerId, pair.BaseAssetCode);
        ValidateAccount(quoteAccount, ownerType, ownerId, pair.QuoteAssetCode);
        return new OwnerAccounts(baseAccount, quoteAccount);
    }

    private static void ValidateAccount(FinancialAccount account, FinancialAccountOwnerType ownerType, Guid ownerId, string assetCode)
    {
        if (account.OwnerType != ownerType || account.OwnerId != ownerId || account.AccountType != FinancialAccountType.Customer) throw new InvalidOperationException("RFQ financial-account ownership is invalid.");
        if (!string.Equals(account.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("RFQ financial-account asset is invalid.");
        if (account.Status != FinancialAccountStatus.Active) throw new InvalidOperationException("RFQ financial account is not active.");
    }

    private static void ValidateQuantity(MarketplacePair pair, decimal quantity)
    {
        if (quantity < pair.MinimumOrderQuantity) throw new InvalidOperationException($"RFQ quantity must be at least {pair.MinimumOrderQuantity}.");
        if (pair.MaximumOrderQuantity.HasValue && quantity > pair.MaximumOrderQuantity.Value) throw new InvalidOperationException($"RFQ quantity cannot exceed {pair.MaximumOrderQuantity.Value}.");
        if (pair.QuantityIncrement > 0m && quantity % pair.QuantityIncrement != 0m) throw new InvalidOperationException($"RFQ quantity must be in increments of {pair.QuantityIncrement}.");
    }

    private static void ValidatePrice(MarketplacePair pair, decimal price)
    {
        if (price <= 0m) throw new InvalidOperationException("RFQ price must be greater than zero.");
        if (pair.PriceIncrement > 0m && price % pair.PriceIncrement != 0m) throw new InvalidOperationException($"RFQ price must be in increments of {pair.PriceIncrement}.");
    }

    private static TradeOrder NewFilledOrder(MarketplacePair pair, FinancialAccountOwnerType ownerType, Guid ownerId, Guid baseAccountId, Guid quoteAccountId, TradeOrderSide side, decimal quantity, decimal price, DateTime now, Guid? actionedByUserId) => new()
    {
        Reference = GenerateReference("KXORD"), MarketplacePairId = pair.Id, MarketplacePair = pair, OwnerType = ownerType, OwnerId = ownerId,
        BaseFinancialAccountId = baseAccountId, QuoteFinancialAccountId = quoteAccountId, Side = side, OrderType = TradeOrderType.Limit,
        TimeInForce = TradeOrderTimeInForce.FillOrKill, Status = TradeOrderStatus.Filled, OriginalQuantity = quantity, RemainingQuantity = 0m,
        FilledQuantity = quantity, LimitPrice = price, AverageFillPrice = price, OpenedAt = now, FilledAt = now, CreatedByUserId = actionedByUserId
    };

    private static BusinessTradingRfqDto ToDto(BusinessTradingRfq rfq) => new(rfq.Id, rfq.Reference, rfq.MarketplacePairId, rfq.MarketplacePair.Code, rfq.RequesterOwnerType, rfq.RequesterOwnerId, rfq.CounterpartyOwnerType, rfq.CounterpartyOwnerId, rfq.Side, rfq.Quantity, rfq.Status, rfq.ExpiresAt, rfq.AcceptedQuoteId, rfq.TradeMatchId, rfq.CreatedAt, rfq.AcceptedAt, rfq.Quotes.OrderByDescending(x => x.CreatedAt).Select(x => new BusinessTradingRfqQuoteDto(x.Id, x.Reference, x.Price, x.Status, x.ExpiresAt, x.CreatedAt)).ToList());
    private static string GenerateReference(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];
    private sealed record OwnerAccounts(FinancialAccount Base, FinancialAccount Quote);
}
