using System.Data;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Marketplace;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Marketplace;

public class MarketplaceSettlementService : IMarketplaceSettlementService
{
    private const string IdempotencyScope = "MarketplaceSettlement";
    private readonly AppDbContext _db;

    public MarketplaceSettlementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SettleMatchAsync(Guid tradeMatchId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var existingTrade = await _db.Trades
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TradeMatchId == tradeMatchId && !x.IsDeleted, ct);

            var match = await _db.TradeMatches
                .Include(x => x.MarketplacePair)
                .Include(x => x.BuyOrder)
                .Include(x => x.SellOrder)
                .FirstOrDefaultAsync(x => x.Id == tradeMatchId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Marketplace trade match not found.");

            if (existingTrade is not null)
            {
                if (existingTrade.Status == TradeStatus.Completed &&
                    match.Status == TradeMatchStatus.Settled)
                {
                    await tx.CommitAsync(ct);
                    return;
                }

                throw new InvalidOperationException(
                    "A trade already exists for this match but settlement is not complete.");
            }

            if (match.Status == TradeMatchStatus.Settled)
            {
                throw new InvalidOperationException(
                    "Trade match is marked settled but no completed trade exists.");
            }

            if (match.Status is TradeMatchStatus.Failed or TradeMatchStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Trade match cannot be settled while its status is '{match.Status}'.");
            }

            ValidateMatchIdentity(match);

            var buyOrder = match.BuyOrder
                ?? throw new InvalidOperationException("Buy order was not loaded for settlement.");
            var sellOrder = match.SellOrder
                ?? throw new InvalidOperationException("Sell order was not loaded for settlement.");
            var pair = match.MarketplacePair
                ?? throw new InvalidOperationException("Marketplace pair was not loaded for settlement.");

            if (!buyOrder.ReservationId.HasValue || !sellOrder.ReservationId.HasValue)
            {
                throw new InvalidOperationException("Both matched orders must have financial reservations.");
            }

            var reservations = await _db.FinancialReservations
                .Where(x =>
                    (x.Id == buyOrder.ReservationId.Value ||
                     x.Id == sellOrder.ReservationId.Value) &&
                    !x.IsDeleted)
                .ToListAsync(ct);

            var buyReservation = reservations.SingleOrDefault(x => x.Id == buyOrder.ReservationId.Value)
                ?? throw new InvalidOperationException("Buy-order reservation not found.");
            var sellReservation = reservations.SingleOrDefault(x => x.Id == sellOrder.ReservationId.Value)
                ?? throw new InvalidOperationException("Sell-order reservation not found.");

            ValidateReservation(buyReservation, buyOrder, buyOrder.QuoteFinancialAccountId);
            ValidateReservation(sellReservation, sellOrder, sellOrder.BaseFinancialAccountId);

            var accountIds = new[]
            {
                buyOrder.BaseFinancialAccountId,
                buyOrder.QuoteFinancialAccountId,
                sellOrder.BaseFinancialAccountId,
                sellOrder.QuoteFinancialAccountId
            }.Distinct().ToArray();

            var accounts = await _db.FinancialAccounts
                .Where(x => accountIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, ct);

            var buyerBase = GetAccount(accounts, buyOrder.BaseFinancialAccountId, "buyer base");
            var buyerQuote = GetAccount(accounts, buyOrder.QuoteFinancialAccountId, "buyer quote");
            var sellerBase = GetAccount(accounts, sellOrder.BaseFinancialAccountId, "seller base");
            var sellerQuote = GetAccount(accounts, sellOrder.QuoteFinancialAccountId, "seller quote");

            ValidateAccount(buyerBase, buyOrder, pair.BaseAssetCode, "buyer base");
            ValidateAccount(buyerQuote, buyOrder, pair.QuoteAssetCode, "buyer quote");
            ValidateAccount(sellerBase, sellOrder, pair.BaseAssetCode, "seller base");
            ValidateAccount(sellerQuote, sellOrder, pair.QuoteAssetCode, "seller quote");

            var now = DateTime.UtcNow;
            match.Status = TradeMatchStatus.SettlementPending;
            match.SettlementStartedAt = now;
            match.LastUpdatedAt = now;

            var trade = new Trade
            {
                Reference = GenerateReference("KXTRD"),
                TradeMatchId = match.Id,
                TradeMatch = match,
                MarketplacePairId = pair.Id,
                MarketplacePair = pair,
                BuyerBaseFinancialAccountId = buyerBase.Id,
                BuyerBaseFinancialAccount = buyerBase,
                BuyerQuoteFinancialAccountId = buyerQuote.Id,
                BuyerQuoteFinancialAccount = buyerQuote,
                SellerBaseFinancialAccountId = sellerBase.Id,
                SellerBaseFinancialAccount = sellerBase,
                SellerQuoteFinancialAccountId = sellerQuote.Id,
                SellerQuoteFinancialAccount = sellerQuote,
                Price = match.Price,
                BaseQuantity = match.BaseQuantity,
                QuoteQuantity = match.QuoteQuantity,
                Status = TradeStatus.Settling,
                SettlementStartedAt = now
            };

            var baseLedger = CaptureReservedAssetToAccount(
                sellReservation,
                sellerBase,
                buyerBase,
                match.BaseQuantity,
                trade,
                match,
                pair.BaseAssetCode,
                "BASE",
                now);

            var quoteLedger = CaptureReservedAssetToAccount(
                buyReservation,
                buyerQuote,
                sellerQuote,
                match.QuoteQuantity,
                trade,
                match,
                pair.QuoteAssetCode,
                "QUOTE",
                now);

            ReleaseClosedOrderExcess(
                sellOrder,
                sellReservation,
                sellerBase,
                trade,
                match,
                now);

            ReleaseClosedOrderExcess(
                buyOrder,
                buyReservation,
                buyerQuote,
                trade,
                match,
                now);

            trade.BaseLedgerTransactionId = baseLedger.Id;
            trade.BaseLedgerTransaction = baseLedger;
            trade.QuoteLedgerTransactionId = quoteLedger.Id;
            trade.QuoteLedgerTransaction = quoteLedger;
            trade.Status = TradeStatus.Completed;
            trade.CompletedAt = now;
            trade.LastUpdatedAt = now;

            match.Status = TradeMatchStatus.Settled;
            match.SettledAt = now;
            match.LastUpdatedAt = now;

            _db.Trades.Add(trade);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private LedgerTransaction CaptureReservedAssetToAccount(
        FinancialReservation reservation,
        FinancialAccount source,
        FinancialAccount destination,
        decimal amount,
        Trade trade,
        TradeMatch match,
        string assetCode,
        string legName,
        DateTime now)
    {
        if (amount <= 0m)
        {
            throw new InvalidOperationException("Marketplace settlement amount must be greater than zero.");
        }

        if (reservation.Status != FinancialReservationStatus.Active)
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.Reference} is not active.");
        }

        var remaining = reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount;
        if (remaining < amount)
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.Reference} does not have enough remaining funds.");
        }

        if (source.HeldBalance < amount || source.SettledBalance < amount)
        {
            throw new InvalidOperationException(
                $"Source {assetCode} account balances are inconsistent with the marketplace reservation.");
        }

        if (destination.Status != FinancialAccountStatus.Active)
        {
            throw new InvalidOperationException(
                $"Destination {assetCode} financial account is not active.");
        }

        if (!string.Equals(source.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(destination.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Marketplace {legName.ToLowerInvariant()} settlement account asset mismatch.");
        }

        source.HeldBalance -= amount;
        source.SettledBalance -= amount;
        source.LastUpdatedAt = now;

        destination.SettledBalance += amount;
        destination.AvailableBalance += amount;
        destination.LastUpdatedAt = now;

        reservation.CapturedAmount += amount;
        reservation.LastUpdatedAt = now;

        if (reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount <= 0m)
        {
            reservation.Status = FinancialReservationStatus.Captured;
            reservation.CapturedAt = now;
        }

        var ledger = new LedgerTransaction
        {
            Reference = GenerateReference("KXLED"),
            AssetCode = assetCode,
            Type = LedgerTransactionType.Trade,
            Status = LedgerTransactionStatus.Posted,
            Amount = amount,
            Description = $"Marketplace trade {trade.Reference} {legName.ToLowerInvariant()} settlement for match {match.Reference}.",
            IdempotencyScope = IdempotencyScope,
            IdempotencyKey = $"{match.Id:N}:{legName}",
            RelatedEntityType = nameof(Trade),
            RelatedEntityId = trade.Id,
            ContextEntityType = nameof(TradeMatch),
            ContextEntityId = match.Id,
            PostedAt = now
        };

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = source.Id,
            FinancialAccount = source,
            BalanceBucket = LedgerBalanceBucket.Held,
            Side = LedgerPostingSide.Debit,
            Amount = amount,
            AccountBalanceAfter = source.HeldBalance
        });

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = destination.Id,
            FinancialAccount = destination,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = amount,
            AccountBalanceAfter = destination.AvailableBalance
        });

        _db.LedgerTransactions.Add(ledger);
        return ledger;
    }

    private void ReleaseClosedOrderExcess(
        TradeOrder order,
        FinancialReservation reservation,
        FinancialAccount account,
        Trade trade,
        TradeMatch match,
        DateTime now)
    {
        if (order.Status != TradeOrderStatus.Filled ||
            reservation.Status != FinancialReservationStatus.Active)
        {
            return;
        }

        var remaining = reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount;
        if (remaining <= 0m)
        {
            return;
        }

        if (account.HeldBalance < remaining)
        {
            throw new InvalidOperationException(
                $"Held balance is inconsistent while closing reservation {reservation.Reference}.");
        }

        account.HeldBalance -= remaining;
        account.AvailableBalance += remaining;
        account.LastUpdatedAt = now;

        reservation.ReleasedAmount += remaining;
        reservation.ReleasedAt = now;
        reservation.ReleaseReason =
            $"Released residual marketplace hold after order {order.Reference} was fully filled.";
        reservation.LastUpdatedAt = now;

        if (reservation.Amount - reservation.CapturedAmount - reservation.ReleasedAmount <= 0m)
        {
            reservation.Status = reservation.CapturedAmount > 0m
                ? FinancialReservationStatus.Captured
                : FinancialReservationStatus.Released;

            if (reservation.CapturedAmount > 0m)
            {
                reservation.CapturedAt ??= now;
            }
        }

        var ledger = new LedgerTransaction
        {
            Reference = GenerateReference("KXLED"),
            AssetCode = account.AssetCode,
            Type = LedgerTransactionType.ReservationRelease,
            Status = LedgerTransactionStatus.Posted,
            Amount = remaining,
            Description =
                $"Released residual marketplace hold for order {order.Reference} after match {match.Reference}.",
            IdempotencyScope = IdempotencyScope,
            IdempotencyKey = $"{match.Id:N}:RELEASE:{order.Id:N}",
            RelatedEntityType = nameof(TradeOrder),
            RelatedEntityId = order.Id,
            ContextEntityType = nameof(Trade),
            ContextEntityId = trade.Id,
            PostedAt = now
        };

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            BalanceBucket = LedgerBalanceBucket.Held,
            Side = LedgerPostingSide.Debit,
            Amount = remaining,
            AccountBalanceAfter = account.HeldBalance
        });

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = remaining,
            AccountBalanceAfter = account.AvailableBalance
        });

        _db.LedgerTransactions.Add(ledger);
    }

    private static void ValidateMatchIdentity(TradeMatch match)
    {
        if (match.BuyOrderId == match.SellOrderId)
        {
            throw new InvalidOperationException("A marketplace match cannot reference the same order twice.");
        }

        if (match.MakerOrderId == match.TakerOrderId)
        {
            throw new InvalidOperationException("Marketplace maker and taker orders must be different.");
        }

        var makerValid = match.MakerOrderId == match.BuyOrderId || match.MakerOrderId == match.SellOrderId;
        var takerValid = match.TakerOrderId == match.BuyOrderId || match.TakerOrderId == match.SellOrderId;
        if (!makerValid || !takerValid)
        {
            throw new InvalidOperationException("Marketplace maker/taker identifiers are inconsistent with the match.");
        }

        if (match.BuyOrder is null || match.SellOrder is null)
        {
            throw new InvalidOperationException("Matched orders were not loaded.");
        }

        if (match.BuyOrder.Side != TradeOrderSide.Buy ||
            match.SellOrder.Side != TradeOrderSide.Sell)
        {
            throw new InvalidOperationException("Marketplace match order sides are invalid.");
        }

        if (match.BuyOrder.MarketplacePairId != match.MarketplacePairId ||
            match.SellOrder.MarketplacePairId != match.MarketplacePairId)
        {
            throw new InvalidOperationException("Marketplace match orders are not on the same pair.");
        }

        if (match.BuyOrder.OwnerType == match.SellOrder.OwnerType &&
            match.BuyOrder.OwnerId == match.SellOrder.OwnerId)
        {
            throw new InvalidOperationException("Self-matched marketplace trades cannot be settled.");
        }

        if (match.BaseQuantity <= 0m || match.QuoteQuantity <= 0m || match.Price <= 0m)
        {
            throw new InvalidOperationException("Marketplace match quantities and price must be positive.");
        }
    }

    private static void ValidateReservation(
        FinancialReservation reservation,
        TradeOrder order,
        Guid expectedFinancialAccountId)
    {
        if (reservation.Type != FinancialReservationType.MarketplaceTrade ||
            reservation.RelatedEntityType != nameof(TradeOrder) ||
            reservation.RelatedEntityId != order.Id)
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.Reference} is not linked to order {order.Reference}.");
        }

        if (reservation.FinancialAccountId != expectedFinancialAccountId)
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.Reference} is linked to the wrong financial account.");
        }
    }

    private static FinancialAccount GetAccount(
        IReadOnlyDictionary<Guid, FinancialAccount> accounts,
        Guid id,
        string label)
    {
        return accounts.TryGetValue(id, out var account)
            ? account
            : throw new InvalidOperationException($"Marketplace {label} financial account not found.");
    }

    private static void ValidateAccount(
        FinancialAccount account,
        TradeOrder order,
        string expectedAssetCode,
        string label)
    {
        if (account.OwnerType != order.OwnerType || account.OwnerId != order.OwnerId)
        {
            throw new InvalidOperationException(
                $"Marketplace {label} account ownership does not match its order.");
        }

        if (!string.Equals(account.AssetCode, expectedAssetCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Marketplace {label} account asset does not match the pair.");
        }

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new InvalidOperationException(
                $"Marketplace {label} account is not active.");
        }
    }

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];
}
