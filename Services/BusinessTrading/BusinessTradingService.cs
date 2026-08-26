using KorridorX.Data;
using KorridorX.Dtos.BusinessTrading;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Instant;
using KorridorX.Services.Marketplace;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTrading;

public sealed class BusinessTradingService : IBusinessTradingService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _access;
    private readonly IMarketplaceOrderService _orders;
    private readonly IMarketplaceOperationsService _marketplace;
    private readonly IInstantTradingService _instant;
    private readonly IBusinessTradingRfqService _rfqs;

    public BusinessTradingService(
        AppDbContext db,
        IBusinessAccessService access,
        IMarketplaceOrderService orders,
        IMarketplaceOperationsService marketplace,
        IInstantTradingService instant,
        IBusinessTradingRfqService rfqs)
    {
        _db = db;
        _access = access;
        _orders = orders;
        _marketplace = marketplace;
        _instant = instant;
        _rfqs = rfqs;
    }

    public async Task<BusinessTradingWorkspaceDto> GetWorkspaceAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: false, ct);
        var countryCode = TradingCountry(profile);

        var balancesTask = GetBalancesForProfileAsync(access.BusinessProfileId, ct);
        var marketplaceTask = GetMarketplacePairsForCountryAsync(countryCode, ct);
        var instantTask = GetInstantPairsForCountryAsync(countryCode, ct);

        await Task.WhenAll(balancesTask, marketplaceTask, instantTask);

        return new BusinessTradingWorkspaceDto(
            await balancesTask,
            await marketplaceTask,
            await instantTask);
    }

    public async Task<IReadOnlyList<BusinessTradingBalanceDto>> GetBalancesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await GetBalancesForProfileAsync(access.BusinessProfileId, ct);
    }

    public async Task<IReadOnlyList<MarketplacePairDto>> GetMarketplacePairsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);
        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: false, ct);
        return await GetMarketplacePairsForCountryAsync(TradingCountry(profile), ct);
    }

    public async Task<OrderBookDto> GetOrderBookAsync(
        Guid userId,
        Guid pairId,
        int depth = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);
        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: false, ct);
        await EnsureMarketplaceCountryAccessAsync(TradingCountry(profile), pairId, ct);
        return await _orders.GetOrderBookAsync(pairId, depth, ct);
    }

    public async Task<TradeOrderDto> CreateMarketplaceOrderAsync(
        Guid userId,
        CreateTradeOrderRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);
        await EnsureMarketplaceCountryAccessAsync(
            TradingCountry(profile),
            request.MarketplacePairId,
            ct);

        return await _orders.CreateOrderForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            userId,
            request,
            ct);
    }

    public async Task<TradeOrderDto> GetMarketplaceOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _orders.GetOrderForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            orderId,
            ct);
    }

    public async Task<TradeOrderDto> CancelMarketplaceOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);

        return await _orders.CancelOrderForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            userId,
            orderId,
            ct);
    }

    public async Task<PagedResult<TradeOrderDto>> GetMarketplaceOrdersAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _orders.GetOrdersForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            page,
            pageSize,
            ct);
    }

    public async Task<PagedResult<TradeHistoryDto>> GetMarketplaceTradesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _marketplace.GetTradesForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            page,
            pageSize,
            ct);
    }

    public async Task<IReadOnlyList<InstantPairDto>> GetInstantPairsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);
        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: false, ct);
        return await GetInstantPairsForCountryAsync(TradingCountry(profile), ct);
    }

    public async Task<InstantQuoteDto> CreateInstantQuoteAsync(
        Guid userId,
        CreateInstantQuoteRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        var profile = await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);

        return await _instant.CreateQuoteForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            TradingCountry(profile),
            request,
            businessProfileId: null,
            actionedByUserId: userId,
            ct);
    }

    public async Task<InstantTradeDto> ExecuteInstantQuoteAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);

        return await _instant.ExecuteQuoteForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            quoteId,
            userId,
            ct);
    }

    public async Task<PagedResult<InstantTradeDto>> GetInstantTradesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _instant.GetTradesForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            page,
            pageSize,
            ct);
    }

    public async Task<IReadOnlyList<BusinessTradingCounterpartyDto>> SearchCounterpartiesAsync(
        Guid userId,
        string? search = null,
        Guid? marketplacePairId = null,
        int take = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        take = Math.Clamp(take, 1, 50);
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        string? baseAssetCode = null;
        string? quoteAssetCode = null;

        if (marketplacePairId.HasValue)
        {
            var pair = await _db.MarketplacePairs
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == marketplacePairId.Value &&
                    x.Status == MarketplacePairStatus.Active &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException("Active marketplace pair not found.");

            baseAssetCode = pair.BaseAssetCode;
            quoteAssetCode = pair.QuoteAssetCode;
        }

        var query = _db.BusinessProfiles
            .AsNoTracking()
            .Where(x =>
                x.Id != access.BusinessProfileId &&
                x.KybStatus == KybStatus.Approved &&
                !x.IsDeleted);

        if (normalizedSearch is not null)
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.BusinessName, $"%{normalizedSearch}%") ||
                (x.TradingName != null &&
                 EF.Functions.ILike(x.TradingName, $"%{normalizedSearch}%")));
        }

        if (baseAssetCode is not null && quoteAssetCode is not null)
        {
            query = query.Where(x =>
                _db.CountryAssets.Any(ca =>
                    ca.CountryCode == (x.OperatingCountryCode ?? x.CountryCode) &&
                    ca.AssetCode == baseAssetCode &&
                    ca.CanTrade) &&
                _db.CountryAssets.Any(ca =>
                    ca.CountryCode == (x.OperatingCountryCode ?? x.CountryCode) &&
                    ca.AssetCode == quoteAssetCode &&
                    ca.CanTrade));
        }

        return await query
            .OrderBy(x => x.BusinessName)
            .Take(take)
            .Select(x => new BusinessTradingCounterpartyDto(
                x.Id,
                x.BusinessName,
                x.TradingName,
                x.OperatingCountryCode ?? x.CountryCode))
            .ToListAsync(ct);
    }

    public async Task<BusinessTradingRfqDto> CreateRfqAsync(
        Guid userId,
        CreateBusinessProfileTradingRfqRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        var requester = await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);

        if (request.CounterpartyBusinessProfileId == access.BusinessProfileId)
            throw new InvalidOperationException("A business cannot create an RFQ with itself.");

        var counterparty = await GetProfileAsync(
            request.CounterpartyBusinessProfileId,
            requireApproved: true,
            ct);

        await EnsureMarketplaceCountryAccessAsync(
            TradingCountry(requester),
            request.MarketplacePairId,
            ct);
        await EnsureMarketplaceCountryAccessAsync(
            TradingCountry(counterparty),
            request.MarketplacePairId,
            ct);

        return await _rfqs.CreateAsync(
            FinancialAccountOwnerType.Business,
            requester.Id,
            FinancialAccountOwnerType.Business,
            counterparty.Id,
            userId,
            request.MarketplacePairId,
            request.Side,
            request.Quantity,
            request.ExpiresAt,
            ct);
    }

    public async Task<BusinessTradingRfqDto> QuoteRfqAsync(
        Guid userId,
        Guid rfqId,
        CreateBusinessProfileTradingRfqQuoteRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);

        return await _rfqs.QuoteAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            userId,
            rfqId,
            request.Price,
            request.ExpiresAt,
            ct);
    }

    public async Task<BusinessTradingRfqDto> AcceptRfqQuoteAsync(
        Guid userId,
        Guid rfqId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);
        await GetProfileAsync(access.BusinessProfileId, requireApproved: true, ct);

        return await _rfqs.AcceptAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            userId,
            rfqId,
            quoteId,
            ct);
    }

    public async Task<BusinessTradingRfqDto> CancelRfqAsync(
        Guid userId,
        Guid rfqId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.Trade,
            ct);

        return await _rfqs.CancelAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            userId,
            rfqId,
            ct);
    }

    public async Task<BusinessTradingRfqDto> GetRfqAsync(
        Guid userId,
        Guid rfqId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _rfqs.GetAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            rfqId,
            ct);
    }

    public async Task<PagedResult<BusinessTradingRfqDto>> GetRfqsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            BusinessPermission.ViewTrading,
            ct);

        return await _rfqs.GetForOwnerAsync(
            FinancialAccountOwnerType.Business,
            access.BusinessProfileId,
            page,
            pageSize,
            ct);
    }

    private async Task<IReadOnlyList<BusinessTradingBalanceDto>> GetBalancesForProfileAsync(
        Guid businessProfileId,
        CancellationToken ct)
    {
        return await (
            from account in _db.FinancialAccounts.AsNoTracking()
            join asset in _db.Assets.AsNoTracking()
                on account.AssetCode equals asset.Code
            where account.OwnerType == FinancialAccountOwnerType.Business &&
                  account.OwnerId == businessProfileId &&
                  account.AccountType == FinancialAccountType.Customer &&
                  !account.IsDeleted
            orderby asset.Type, account.AssetCode
            select new BusinessTradingBalanceDto(
                account.Id,
                account.AssetCode,
                asset.Name,
                asset.Type,
                account.Status,
                account.SettledBalance,
                account.AvailableBalance,
                account.HeldBalance))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<MarketplacePairDto>> GetMarketplacePairsForCountryAsync(
        string countryCode,
        CancellationToken ct)
    {
        var pairs = await _orders.GetActivePairsAsync(ct);
        if (pairs.Count == 0) return pairs;

        var assetCodes = pairs
            .SelectMany(x => new[] { x.BaseAssetCode, x.QuoteAssetCode })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowed = await _db.CountryAssets
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == countryCode &&
                assetCodes.Contains(x.AssetCode) &&
                x.CanTrade)
            .Select(x => x.AssetCode)
            .ToListAsync(ct);

        var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return pairs
            .Where(x =>
                allowedSet.Contains(x.BaseAssetCode) &&
                allowedSet.Contains(x.QuoteAssetCode))
            .ToList();
    }

    private async Task<IReadOnlyList<InstantPairDto>> GetInstantPairsForCountryAsync(
        string countryCode,
        CancellationToken ct)
    {
        var pairs = await _instant.GetPairsAsync(ct);
        if (pairs.Count == 0) return pairs;

        var assetCodes = pairs
            .SelectMany(x => new[] { x.SourceAssetCode, x.DestinationAssetCode })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowed = await _db.CountryAssets
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == countryCode &&
                assetCodes.Contains(x.AssetCode) &&
                x.CanTrade)
            .Select(x => x.AssetCode)
            .ToListAsync(ct);

        var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return pairs
            .Where(x =>
                allowedSet.Contains(x.SourceAssetCode) &&
                allowedSet.Contains(x.DestinationAssetCode))
            .ToList();
    }

    private async Task<BusinessProfile> GetProfileAsync(
        Guid businessProfileId,
        bool requireApproved,
        CancellationToken ct)
    {
        var profile = await _db.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == businessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business profile not found.");

        if (requireApproved && profile.KybStatus != KybStatus.Approved)
            throw new InvalidOperationException(
                "Business trading requires an approved KYB profile.");

        return profile;
    }

    private async Task EnsureMarketplaceCountryAccessAsync(
        string countryCode,
        Guid pairId,
        CancellationToken ct)
    {
        var pair = await _db.MarketplacePairs
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == pairId &&
                x.Status == MarketplacePairStatus.Active &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Active marketplace pair not found.");

        var mappings = await _db.CountryAssets
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == countryCode &&
                (x.AssetCode == pair.BaseAssetCode ||
                 x.AssetCode == pair.QuoteAssetCode))
            .ToListAsync(ct);

        if (!mappings.Any(x =>
                x.AssetCode == pair.BaseAssetCode &&
                x.CanTrade) ||
            !mappings.Any(x =>
                x.AssetCode == pair.QuoteAssetCode &&
                x.CanTrade))
        {
            throw new InvalidOperationException(
                "Marketplace trading is not enabled for one or more assets in the business country.");
        }
    }

    private static string TradingCountry(BusinessProfile profile) =>
        (profile.OperatingCountryCode ?? profile.CountryCode)
            .Trim()
            .ToUpperInvariant();
}