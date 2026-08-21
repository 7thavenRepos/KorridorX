using System.Data;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Instant;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Instant;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KorridorX.Services.Instant;

public sealed class InstantTradingService : IInstantTradingService
{
    private const string SettlementScope = "InstantTradeSettlement";
    private const int MaxExecutionAttempts = 3;

    private readonly AppDbContext _db;
    private readonly IFinancialReservationService _reservations;
    private readonly TreasuryOptions _treasuryOptions;

    public InstantTradingService(
        AppDbContext db,
        IFinancialReservationService reservations,
        IOptions<TreasuryOptions> treasuryOptions)
    {
        _db = db;
        _reservations = reservations;
        _treasuryOptions = treasuryOptions.Value;
    }

    public async Task<IReadOnlyList<InstantPairDto>> GetPairsAsync(CancellationToken ct = default)
    {
        return await _db.InstantPairs
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == InstantPairStatus.Active)
            .OrderBy(x => x.Code)
            .Select(x => ToPairDto(x))
            .ToListAsync(ct);
    }

    public async Task<InstantQuoteDto> CreateQuoteAsync(
        Guid userId,
        CreateInstantQuoteRequestDto request,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var pair = await _db.InstantPairs
            .AsNoTracking()
            .Include(x => x.SourceAsset)
            .Include(x => x.DestinationAsset)
            .Include(x => x.HouseSourceFinancialAccount)
            .Include(x => x.HouseDestinationFinancialAccount)
            .FirstOrDefaultAsync(x => x.Id == request.InstantPairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Instant trading pair not found.");

        if (pair.Status != InstantPairStatus.Active)
            throw new InvalidOperationException("Instant trading is not active for this pair.");

        ValidateSourceAmount(pair, request.SourceAmount);

        if (!pair.SourceAsset.IsSupported || !pair.SourceAsset.InstantEnabled ||
            !pair.DestinationAsset.IsSupported || !pair.DestinationAsset.InstantEnabled)
            throw new InvalidOperationException("One or more assets are not enabled for instant trading.");

        ValidateHouseAccount(pair.HouseSourceFinancialAccount, pair.SourceAssetCode, "source");
        ValidateHouseAccount(pair.HouseDestinationFinancialAccount, pair.DestinationAssetCode, "destination");

        var profile = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Customer profile not found.");

        if (profile.KycStatus != KycStatus.Approved)
            throw new InvalidOperationException("Approved KYC is required for instant trading.");

        var countryAssets = await _db.CountryAssets
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == profile.CountryCode &&
                (x.AssetCode == pair.SourceAssetCode || x.AssetCode == pair.DestinationAssetCode))
            .ToListAsync(ct);

        if (!countryAssets.Any(x => x.AssetCode == pair.SourceAssetCode && x.CanUseInstant) ||
            !countryAssets.Any(x => x.AssetCode == pair.DestinationAssetCode && x.CanUseInstant))
            throw new InvalidOperationException("Instant trading is not enabled for one or more assets in the customer's country.");

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                x.AccountType == FinancialAccountType.Customer &&
                (x.AssetCode == pair.SourceAssetCode || x.AssetCode == pair.DestinationAssetCode) &&
                !x.IsDeleted)
            .ToListAsync(ct);

        var sourceAccount = accounts.SingleOrDefault(x => x.AssetCode == pair.SourceAssetCode)
            ?? throw new InvalidOperationException($"Customer {pair.SourceAssetCode} account not found.");
        var destinationAccount = accounts.SingleOrDefault(x => x.AssetCode == pair.DestinationAssetCode)
            ?? throw new InvalidOperationException($"Customer {pair.DestinationAssetCode} account not found.");

        if (sourceAccount.Status != FinancialAccountStatus.Active ||
            destinationAccount.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Both customer financial accounts must be active.");

        if (sourceAccount.AvailableBalance < request.SourceAmount)
            throw new InvalidOperationException($"Insufficient {pair.SourceAssetCode} available balance.");

        var staleBefore = now.AddMinutes(-_treasuryOptions.FxRateStaleMinutes);
        var rate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.SourceCurrencyCode == pair.SourceAssetCode &&
                x.DestinationCurrencyCode == pair.DestinationAssetCode &&
                x.EffectiveFrom <= now &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > now))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active FX rate is available for this instant pair.");

        if (rate.EffectiveFrom < staleBefore)
            throw new InvalidOperationException("The current instant FX rate is stale.");

        var destinationAmount = decimal.Round(
            request.SourceAmount * rate.CustomerRate,
            pair.DestinationAsset.DecimalPlaces,
            MidpointRounding.ToZero);

        if (destinationAmount <= 0m)
            throw new InvalidOperationException("The calculated instant destination amount is invalid.");

        if (pair.HouseDestinationFinancialAccount.AvailableBalance < destinationAmount ||
            pair.HouseDestinationFinancialAccount.SettledBalance < destinationAmount)
            throw new InvalidOperationException($"House {pair.DestinationAssetCode} liquidity is insufficient.");

        var quote = new InstantQuote
        {
            Reference = GenerateReference("KXIQ"),
            InstantPairId = pair.Id,
            UserId = userId,
            UserSourceFinancialAccountId = sourceAccount.Id,
            UserDestinationFinancialAccountId = destinationAccount.Id,
            HouseSourceFinancialAccountId = pair.HouseSourceFinancialAccountId,
            HouseDestinationFinancialAccountId = pair.HouseDestinationFinancialAccountId,
            ExchangeRateId = rate.Id,
            SourceAmount = request.SourceAmount,
            DestinationAmount = destinationAmount,
            ProviderRate = rate.ProviderRate,
            CustomerRate = rate.CustomerRate,
            Status = InstantQuoteStatus.Active,
            ExpiresAt = now.AddSeconds(pair.QuoteValiditySeconds),
            CreatedByUserId = userId
        };

        _db.InstantQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        return ToQuoteDto(quote, pair);
    }

    public async Task<InstantTradeDto> ExecuteQuoteAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxExecutionAttempts; attempt++)
        {
            try
            {
                return await ExecuteQuoteOnceAsync(userId, quoteId, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxExecutionAttempts)
            {
                _db.ChangeTracker.Clear();
            }
            catch (PostgresException ex) when (
                attempt < MaxExecutionAttempts &&
                (ex.SqlState == PostgresErrorCodes.SerializationFailure ||
                 ex.SqlState == PostgresErrorCodes.UniqueViolation))
            {
                _db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Instant quote execution could not be completed.");
    }

    private async Task<InstantTradeDto> ExecuteQuoteOnceAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var existing = await _db.InstantTrades
            .AsNoTracking()
            .Include(x => x.InstantPair)
            .FirstOrDefaultAsync(x => x.InstantQuoteId == quoteId && !x.IsDeleted, ct);

        if (existing is not null)
        {
            if (existing.UserId != userId)
                throw new UnauthorizedAccessException("Instant trade does not belong to the authenticated user.");

            if (existing.Status == InstantTradeStatus.Completed)
            {
                await tx.CommitAsync(ct);
                return ToTradeDto(existing);
            }

            throw new InvalidOperationException(
                "An instant trade already exists for this quote and requires operational review.");
        }

        var quote = await _db.InstantQuotes
            .Include(x => x.InstantPair)
            .Include(x => x.UserSourceFinancialAccount)
            .Include(x => x.UserDestinationFinancialAccount)
            .Include(x => x.HouseSourceFinancialAccount)
            .Include(x => x.HouseDestinationFinancialAccount)
            .FirstOrDefaultAsync(x => x.Id == quoteId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Instant quote not found.");

        if (quote.UserId != userId)
            throw new UnauthorizedAccessException("Instant quote does not belong to the authenticated user.");

        if (quote.Status == InstantQuoteStatus.Consumed)
        {
            var completed = await _db.InstantTrades
                .AsNoTracking()
                .Include(x => x.InstantPair)
                .FirstOrDefaultAsync(x =>
                    x.InstantQuoteId == quote.Id &&
                    x.Status == InstantTradeStatus.Completed &&
                    !x.IsDeleted,
                    ct);

            if (completed is not null)
            {
                await tx.CommitAsync(ct);
                return ToTradeDto(completed);
            }
        }

        if (quote.Status != InstantQuoteStatus.Active)
            throw new InvalidOperationException("Instant quote is no longer active.");

        var now = DateTime.UtcNow;
        if (quote.ExpiresAt <= now)
        {
            quote.Status = InstantQuoteStatus.Expired;
            quote.LastUpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new InvalidOperationException("Instant quote has expired.");
        }

        if (quote.InstantPair.Status != InstantPairStatus.Active)
            throw new InvalidOperationException("Instant trading is currently paused for this pair.");

        ValidateUserAccount(
            quote.UserSourceFinancialAccount,
            userId,
            quote.InstantPair.SourceAssetCode,
            "source");

        ValidateUserAccount(
            quote.UserDestinationFinancialAccount,
            userId,
            quote.InstantPair.DestinationAssetCode,
            "destination");

        ValidateHouseAccount(
            quote.HouseSourceFinancialAccount,
            quote.InstantPair.SourceAssetCode,
            "source");

        ValidateHouseAccount(
            quote.HouseDestinationFinancialAccount,
            quote.InstantPair.DestinationAssetCode,
            "destination");

        if (quote.HouseDestinationFinancialAccount.AvailableBalance < quote.DestinationAmount ||
            quote.HouseDestinationFinancialAccount.SettledBalance < quote.DestinationAmount)
            throw new InvalidOperationException(
                $"House {quote.InstantPair.DestinationAssetCode} liquidity is insufficient.");

        var trade = new InstantTrade
        {
            Reference = GenerateReference("KXIT"),
            InstantQuoteId = quote.Id,
            InstantPairId = quote.InstantPairId,
            UserId = userId,
            UserSourceFinancialAccountId = quote.UserSourceFinancialAccountId,
            UserDestinationFinancialAccountId = quote.UserDestinationFinancialAccountId,
            HouseSourceFinancialAccountId = quote.HouseSourceFinancialAccountId,
            HouseDestinationFinancialAccountId = quote.HouseDestinationFinancialAccountId,
            SourceAmount = quote.SourceAmount,
            DestinationAmount = quote.DestinationAmount,
            CustomerRate = quote.CustomerRate,
            Status = InstantTradeStatus.Settling,
            SettlementStartedAt = now,
            CreatedByUserId = userId
        };

        _db.InstantTrades.Add(trade);

        var reservation = await _reservations.ReserveAsync(
            quote.UserSourceFinancialAccountId,
            FinancialReservationType.InstantTrade,
            nameof(InstantTrade),
            trade.Id,
            quote.SourceAmount,
            userId,
            nameof(InstantQuote),
            quote.Id,
            ct);

        trade.ReservationId = reservation.Id;

        var sourceLedger = SettleSourceLeg(quote, trade, reservation, now);
        var destinationLedger = SettleDestinationLeg(quote, trade, now);

        trade.SourceLedgerTransactionId = sourceLedger.Id;
        trade.SourceLedgerTransaction = sourceLedger;
        trade.DestinationLedgerTransactionId = destinationLedger.Id;
        trade.DestinationLedgerTransaction = destinationLedger;
        trade.Status = InstantTradeStatus.Completed;
        trade.CompletedAt = now;
        trade.LastUpdatedAt = now;

        quote.Status = InstantQuoteStatus.Consumed;
        quote.ConsumedAt = now;
        quote.LastUpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToTradeDto(trade, quote.InstantPair);
    }

    public async Task<PagedResult<InstantTradeDto>> GetMyTradesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InstantTrades
            .AsNoTracking()
            .Include(x => x.InstantPair)
            .Where(x => x.UserId == userId && !x.IsDeleted);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InstantTradeDto>
        {
            Items = items.Select(ToTradeDto).ToList(),
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };
    }

    public async Task<InstantPairDto> CreatePairAsync(
        Guid actionedByUserId,
        CreateInstantPairRequestDto request,
        CancellationToken ct = default)
    {
        var source = NormalizeAssetCode(request.SourceAssetCode);
        var destination = NormalizeAssetCode(request.DestinationAssetCode);

        if (source == destination)
            throw new InvalidOperationException("Instant source and destination assets must be different.");

        ValidatePairSettings(
            request.MinimumSourceAmount,
            request.MaximumSourceAmount,
            request.SourceAmountIncrement,
            request.QuoteValiditySeconds);

        if (await _db.InstantPairs.AnyAsync(x =>
                !x.IsDeleted &&
                (x.Code == $"{source}/{destination}" ||
                 (x.SourceAssetCode == source && x.DestinationAssetCode == destination)), ct))
            throw new InvalidOperationException("Instant trading pair already exists.");

        var assets = await _db.Assets.AsNoTracking()
            .Where(x => x.Code == source || x.Code == destination)
            .ToListAsync(ct);

        if (assets.Count != 2)
            throw new InvalidOperationException("One or more instant trading assets do not exist.");

        var accountIds = new[]
        {
            request.HouseSourceFinancialAccountId,
            request.HouseDestinationFinancialAccountId
        };

        var accounts = await _db.FinancialAccounts
            .Where(x => accountIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        var houseSource = accounts.SingleOrDefault(x => x.Id == request.HouseSourceFinancialAccountId)
            ?? throw new InvalidOperationException("House source financial account not found.");
        var houseDestination = accounts.SingleOrDefault(x => x.Id == request.HouseDestinationFinancialAccountId)
            ?? throw new InvalidOperationException("House destination financial account not found.");

        ValidateHouseAccount(houseSource, source, "source");
        ValidateHouseAccount(houseDestination, destination, "destination");

        var pair = new InstantPair
        {
            Code = $"{source}/{destination}",
            SourceAssetCode = source,
            DestinationAssetCode = destination,
            HouseSourceFinancialAccountId = houseSource.Id,
            HouseDestinationFinancialAccountId = houseDestination.Id,
            MinimumSourceAmount = request.MinimumSourceAmount,
            MaximumSourceAmount = request.MaximumSourceAmount,
            SourceAmountIncrement = request.SourceAmountIncrement,
            QuoteValiditySeconds = request.QuoteValiditySeconds,
            Status = request.Status,
            CreatedByUserId = actionedByUserId
        };

        _db.InstantPairs.Add(pair);
        await _db.SaveChangesAsync(ct);
        return ToPairDto(pair);
    }

    public async Task<InstantPairDto> UpdatePairAsync(
        Guid actionedByUserId,
        Guid pairId,
        UpdateInstantPairRequestDto request,
        CancellationToken ct = default)
    {
        ValidatePairSettings(
            request.MinimumSourceAmount,
            request.MaximumSourceAmount,
            request.SourceAmountIncrement,
            request.QuoteValiditySeconds);

        var pair = await _db.InstantPairs
            .FirstOrDefaultAsync(x => x.Id == pairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Instant trading pair not found.");

        var accountIds = new[]
        {
            request.HouseSourceFinancialAccountId,
            request.HouseDestinationFinancialAccountId
        };

        var accounts = await _db.FinancialAccounts
            .Where(x => accountIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        var houseSource = accounts.SingleOrDefault(x => x.Id == request.HouseSourceFinancialAccountId)
            ?? throw new InvalidOperationException("House source financial account not found.");
        var houseDestination = accounts.SingleOrDefault(x => x.Id == request.HouseDestinationFinancialAccountId)
            ?? throw new InvalidOperationException("House destination financial account not found.");

        ValidateHouseAccount(houseSource, pair.SourceAssetCode, "source");
        ValidateHouseAccount(houseDestination, pair.DestinationAssetCode, "destination");

        pair.HouseSourceFinancialAccountId = houseSource.Id;
        pair.HouseDestinationFinancialAccountId = houseDestination.Id;
        pair.MinimumSourceAmount = request.MinimumSourceAmount;
        pair.MaximumSourceAmount = request.MaximumSourceAmount;
        pair.SourceAmountIncrement = request.SourceAmountIncrement;
        pair.QuoteValiditySeconds = request.QuoteValiditySeconds;
        pair.LastUpdatedAt = DateTime.UtcNow;
        pair.LastUpdatedByUserId = actionedByUserId;

        await _db.SaveChangesAsync(ct);
        return ToPairDto(pair);
    }

    public async Task<InstantPairDto> SetPairStatusAsync(
        Guid actionedByUserId,
        Guid pairId,
        InstantPairStatus status,
        CancellationToken ct = default)
    {
        var pair = await _db.InstantPairs
            .FirstOrDefaultAsync(x => x.Id == pairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Instant trading pair not found.");

        pair.Status = status;
        pair.LastUpdatedAt = DateTime.UtcNow;
        pair.LastUpdatedByUserId = actionedByUserId;
        await _db.SaveChangesAsync(ct);

        return ToPairDto(pair);
    }

    public async Task<PagedResult<InstantQuoteAdminDto>> GetAdminQuotesAsync(
        InstantQuoteStatus? status = null,
        Guid? pairId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InstantQuotes
            .AsNoTracking()
            .Include(x => x.InstantPair)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (pairId.HasValue)
            query = query.Where(x => x.InstantPairId == pairId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InstantQuoteAdminDto(
                x.Id,
                x.Reference,
                x.UserId,
                x.InstantPairId,
                x.InstantPair.Code,
                x.SourceAmount,
                x.DestinationAmount,
                x.CustomerRate,
                x.Status,
                x.ExpiresAt,
                x.CreatedAt,
                x.ConsumedAt))
            .ToListAsync(ct);

        return new PagedResult<InstantQuoteAdminDto>
        {
            Items = items,
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };
    }

    public async Task<PagedResult<InstantTradeDto>> GetAdminTradesAsync(
        InstantTradeStatus? status = null,
        Guid? pairId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InstantTrades
            .AsNoTracking()
            .Include(x => x.InstantPair)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (pairId.HasValue)
            query = query.Where(x => x.InstantPairId == pairId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InstantTradeDto>
        {
            Items = items.Select(ToTradeDto).ToList(),
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };
    }

    public async Task<IReadOnlyList<InstantLiquidityDto>> GetLiquidityAsync(
        CancellationToken ct = default)
    {
        return await _db.InstantPairs
            .AsNoTracking()
            .Include(x => x.HouseSourceFinancialAccount)
            .Include(x => x.HouseDestinationFinancialAccount)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Code)
            .Select(x => new InstantLiquidityDto(
                x.Id,
                x.Code,
                x.HouseSourceFinancialAccountId,
                x.SourceAssetCode,
                x.HouseSourceFinancialAccount.AvailableBalance,
                x.HouseSourceFinancialAccount.HeldBalance,
                x.HouseSourceFinancialAccount.SettledBalance,
                x.HouseDestinationFinancialAccountId,
                x.DestinationAssetCode,
                x.HouseDestinationFinancialAccount.AvailableBalance,
                x.HouseDestinationFinancialAccount.HeldBalance,
                x.HouseDestinationFinancialAccount.SettledBalance,
                x.Status))
            .ToListAsync(ct);
    }

    public async Task<InstantQuoteMaintenanceResultDto> ExpireQuotesAsync(
        int take = 500,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 5000);
        var now = DateTime.UtcNow;

        var ids = await _db.InstantQuotes
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Status == InstantQuoteStatus.Active &&
                x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var expired = 0;
        var failures = new List<InstantOperationFailureDto>();

        foreach (var id in ids)
        {
            try
            {
                var quote = await _db.InstantQuotes
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

                if (quote is null ||
                    quote.Status != InstantQuoteStatus.Active ||
                    quote.ExpiresAt > DateTime.UtcNow)
                {
                    _db.ChangeTracker.Clear();
                    continue;
                }

                quote.Status = InstantQuoteStatus.Expired;
                quote.LastUpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                expired++;
                _db.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                failures.Add(new InstantOperationFailureDto(id, ex.Message));
            }
        }

        return new InstantQuoteMaintenanceResultDto(ids.Count, expired, failures);
    }

    public async Task<IReadOnlyList<InstantSettlementExceptionDto>> GetSettlementExceptionsAsync(
        int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 1000);
        var staleBefore = DateTime.UtcNow.AddMinutes(-5);

        return await _db.InstantTrades
            .AsNoTracking()
            .Include(x => x.InstantPair)
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == InstantTradeStatus.Failed ||
                 ((x.Status == InstantTradeStatus.PendingSettlement ||
                   x.Status == InstantTradeStatus.Settling) &&
                  x.CreatedAt <= staleBefore)))
            .OrderBy(x => x.Status)
            .ThenBy(x => x.CreatedAt)
            .Take(take)
            .Select(x => new InstantSettlementExceptionDto(
                x.Id,
                x.Reference,
                x.InstantQuoteId,
                x.InstantPairId,
                x.InstantPair.Code,
                x.Status,
                x.CreatedAt,
                x.SettlementStartedAt,
                x.FailedAt,
                x.FailureReason))
            .ToListAsync(ct);
    }

    private LedgerTransaction SettleSourceLeg(
        InstantQuote quote,
        InstantTrade trade,
        FinancialReservation reservation,
        DateTime now)
    {
        var source = quote.UserSourceFinancialAccount;
        var house = quote.HouseSourceFinancialAccount;
        var amount = quote.SourceAmount;

        if (reservation.Status != FinancialReservationStatus.Active ||
            reservation.RemainingAmount < amount ||
            source.HeldBalance < amount ||
            source.SettledBalance < amount)
            throw new InvalidOperationException("Instant source reservation is inconsistent.");

        source.HeldBalance -= amount;
        source.SettledBalance -= amount;
        source.LastUpdatedAt = now;

        house.AvailableBalance += amount;
        house.SettledBalance += amount;
        house.LastUpdatedAt = now;

        reservation.CapturedAmount += amount;
        reservation.Status = FinancialReservationStatus.Captured;
        reservation.CapturedAt = now;
        reservation.LastUpdatedAt = now;

        return AddSettlementLedger(
            quote.InstantPair.SourceAssetCode,
            amount,
            trade,
            "SOURCE",
            source,
            LedgerBalanceBucket.Held,
            house,
            LedgerBalanceBucket.Available,
            now);
    }

    private LedgerTransaction SettleDestinationLeg(
        InstantQuote quote,
        InstantTrade trade,
        DateTime now)
    {
        var house = quote.HouseDestinationFinancialAccount;
        var destination = quote.UserDestinationFinancialAccount;
        var amount = quote.DestinationAmount;

        if (house.AvailableBalance < amount || house.SettledBalance < amount)
            throw new InvalidOperationException("House destination liquidity is insufficient.");

        house.AvailableBalance -= amount;
        house.SettledBalance -= amount;
        house.LastUpdatedAt = now;

        destination.AvailableBalance += amount;
        destination.SettledBalance += amount;
        destination.LastUpdatedAt = now;

        return AddSettlementLedger(
            quote.InstantPair.DestinationAssetCode,
            amount,
            trade,
            "DESTINATION",
            house,
            LedgerBalanceBucket.Available,
            destination,
            LedgerBalanceBucket.Available,
            now);
    }

    private LedgerTransaction AddSettlementLedger(
        string assetCode,
        decimal amount,
        InstantTrade trade,
        string leg,
        FinancialAccount debitAccount,
        LedgerBalanceBucket debitBucket,
        FinancialAccount creditAccount,
        LedgerBalanceBucket creditBucket,
        DateTime now)
    {
        var ledger = new LedgerTransaction
        {
            Reference = GenerateReference("KXLED"),
            AssetCode = assetCode,
            Type = LedgerTransactionType.Conversion,
            Status = LedgerTransactionStatus.Posted,
            Amount = amount,
            Description = $"Instant trade {trade.Reference} {leg.ToLowerInvariant()} leg.",
            IdempotencyScope = SettlementScope,
            IdempotencyKey = $"{trade.Id:N}:{leg}",
            RelatedEntityType = nameof(InstantTrade),
            RelatedEntityId = trade.Id,
            ContextEntityType = nameof(InstantQuote),
            ContextEntityId = trade.InstantQuoteId,
            PostedAt = now,
            CreatedByUserId = trade.UserId
        };

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = debitAccount.Id,
            FinancialAccount = debitAccount,
            BalanceBucket = debitBucket,
            Side = LedgerPostingSide.Debit,
            Amount = amount,
            AccountBalanceAfter = debitBucket == LedgerBalanceBucket.Held
                ? debitAccount.HeldBalance
                : debitAccount.AvailableBalance
        });

        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransactionId = ledger.Id,
            LedgerTransaction = ledger,
            FinancialAccountId = creditAccount.Id,
            FinancialAccount = creditAccount,
            BalanceBucket = creditBucket,
            Side = LedgerPostingSide.Credit,
            Amount = amount,
            AccountBalanceAfter = creditAccount.AvailableBalance
        });

        _db.LedgerTransactions.Add(ledger);
        return ledger;
    }

    private static void ValidatePairSettings(
        decimal minimum,
        decimal? maximum,
        decimal increment,
        int quoteValiditySeconds)
    {
        if (minimum <= 0m)
            throw new InvalidOperationException("Minimum instant source amount must be greater than zero.");
        if (maximum.HasValue && maximum.Value < minimum)
            throw new InvalidOperationException("Maximum instant source amount cannot be below the minimum.");
        if (increment <= 0m)
            throw new InvalidOperationException("Instant source amount increment must be greater than zero.");
        if (minimum % increment != 0m)
            throw new InvalidOperationException("Minimum instant source amount must align with the increment.");
        if (maximum.HasValue && maximum.Value % increment != 0m)
            throw new InvalidOperationException("Maximum instant source amount must align with the increment.");
        if (quoteValiditySeconds < 5 || quoteValiditySeconds > 300)
            throw new InvalidOperationException("Instant quote validity must be between 5 and 300 seconds.");
    }

    private static void ValidateSourceAmount(InstantPair pair, decimal amount)
    {
        if (amount < pair.MinimumSourceAmount)
            throw new InvalidOperationException("Instant source amount is below the pair minimum.");
        if (pair.MaximumSourceAmount.HasValue && amount > pair.MaximumSourceAmount.Value)
            throw new InvalidOperationException("Instant source amount exceeds the pair maximum.");
        if (amount % pair.SourceAmountIncrement != 0m)
            throw new InvalidOperationException("Instant source amount does not align with the configured increment.");
    }

    private static void ValidateHouseAccount(
        FinancialAccount account,
        string expectedAssetCode,
        string side)
    {
        if (account.AccountType != FinancialAccountType.House ||
            account.OwnerType is not FinancialAccountOwnerType.Treasury and not FinancialAccountOwnerType.Platform)
            throw new InvalidOperationException($"Configured house {side} account is not a House financial account.");

        if (!string.Equals(account.AssetCode, expectedAssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Configured house {side} account has the wrong asset.");

        if (account.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException($"Configured house {side} account is not active.");
    }

    private static void ValidateUserAccount(
        FinancialAccount account,
        Guid userId,
        string expectedAssetCode,
        string side)
    {
        if (account.OwnerType != FinancialAccountOwnerType.User ||
            account.OwnerId != userId ||
            account.AccountType != FinancialAccountType.Customer)
            throw new InvalidOperationException($"Instant {side} account ownership is invalid.");

        if (!string.Equals(account.AssetCode, expectedAssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Instant {side} account has the wrong asset.");

        if (account.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException($"Instant {side} account is not active.");
    }

    private static string NormalizeAssetCode(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20)
            throw new InvalidOperationException("Invalid instant asset code.");
        return normalized;
    }

    private static InstantPairDto ToPairDto(InstantPair x) => new(
        x.Id,
        x.Code,
        x.SourceAssetCode,
        x.DestinationAssetCode,
        x.Status,
        x.MinimumSourceAmount,
        x.MaximumSourceAmount,
        x.SourceAmountIncrement,
        x.QuoteValiditySeconds);

    private static InstantQuoteDto ToQuoteDto(InstantQuote x, InstantPair pair) => new(
        x.Id,
        x.Reference,
        x.InstantPairId,
        pair.Code,
        pair.SourceAssetCode,
        pair.DestinationAssetCode,
        x.SourceAmount,
        x.DestinationAmount,
        x.CustomerRate,
        x.ExpiresAt,
        x.Status);

    private static InstantTradeDto ToTradeDto(InstantTrade x) =>
        ToTradeDto(x, x.InstantPair);

    private static InstantTradeDto ToTradeDto(InstantTrade x, InstantPair pair) => new(
        x.Id,
        x.Reference,
        x.InstantQuoteId,
        x.InstantPairId,
        pair.Code,
        pair.SourceAssetCode,
        pair.DestinationAssetCode,
        x.SourceAmount,
        x.DestinationAmount,
        x.CustomerRate,
        x.Status,
        x.CreatedAt,
        x.CompletedAt);

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
            [..Math.Min(60, prefix.Length + 1 + 14 + 1 + 32)];
}
