using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.Instant;
using KorridorX.Services.Marketplace;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedTradingService : IEmbeddedTradingService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedFinanceContextAccessor _context;
    private readonly IMarketplaceOrderService _orders;
    private readonly IMarketplaceOperationsService _marketplaceOperations;
    private readonly IInstantTradingService _instant;
    private readonly IBusinessTradingRfqService _rfqs;
    private readonly IBusinessPricingService _businessPricing;

    public EmbeddedTradingService(
        AppDbContext db,
        IEmbeddedFinanceContextAccessor context,
        IMarketplaceOrderService orders,
        IMarketplaceOperationsService marketplaceOperations,
        IInstantTradingService instant,
        IBusinessTradingRfqService rfqs,
        IBusinessPricingService businessPricing)
    {
        _db = db;
        _context = context;
        _orders = orders;
        _marketplaceOperations = marketplaceOperations;
        _instant = instant;
        _rfqs = rfqs;
        _businessPricing = businessPricing;
    }

    public async Task<IReadOnlyList<EmbeddedTradingBalanceDto>> GetBalancesAsync(
        Guid businessCustomerId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);

        return await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
                x.OwnerId == customer.Id &&
                x.AccountType == FinancialAccountType.Customer &&
                !x.IsDeleted)
            .OrderBy(x => x.AssetCode)
            .Select(x => new EmbeddedTradingBalanceDto(
                x.Id,
                x.AssetCode,
                x.Status,
                x.SettledBalance,
                x.AvailableBalance,
                x.HeldBalance))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MarketplacePairDto>> GetMarketplacePairsAsync(
        Guid businessCustomerId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        await EnsureCustomerAsync(businessCustomerId, ct);
        return await _orders.GetActivePairsAsync(ct);
    }

    public async Task<OrderBookDto> GetOrderBookAsync(
        Guid businessCustomerId,
        Guid pairId,
        int depth,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        await EnsureCustomerAsync(businessCustomerId, ct);
        return await _orders.GetOrderBookAsync(pairId, depth, ct);
    }

    public async Task<TradeOrderDto> CreateMarketplaceOrderAsync(
        Guid businessCustomerId,
        CreateTradeOrderRequestDto request,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        await EnsureMarketplaceCountryAccessAsync(customer.CountryCode, request.MarketplacePairId, ct);

        return await _orders.CreateOrderForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            null,
            request,
            ct);
    }

    public async Task<TradeOrderDto> GetMarketplaceOrderAsync(
        Guid businessCustomerId,
        Guid orderId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _orders.GetOrderForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            orderId,
            ct);
    }

    public async Task<TradeOrderDto> CancelMarketplaceOrderAsync(
        Guid businessCustomerId,
        Guid orderId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _orders.CancelOrderForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            null,
            orderId,
            ct);
    }

    public async Task<PagedResult<TradeOrderDto>> GetMarketplaceOrdersAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _orders.GetOrdersForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            page,
            pageSize,
            ct);
    }

    public async Task<PagedResult<TradeHistoryDto>> GetMarketplaceTradesAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _marketplaceOperations.GetTradesForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            page,
            pageSize,
            ct);
    }

    public async Task<IReadOnlyList<BusinessPricingPolicyDto>> GetPricingPoliciesAsync(
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TradingRead);
        return await _businessPricing.GetPoliciesAsync(principal.BusinessProfileId, ct);
    }

    public async Task<IReadOnlyList<BusinessPricingPerformanceRowDto>> GetPricingPerformanceAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TradingRead);
        return await _businessPricing.GetPerformanceAsync(principal.BusinessProfileId, from, to, ct);
    }

    public async Task<BusinessPricingPolicyDto> CreatePricingPolicyAsync(
        CreateBusinessPricingPolicyRequestDto request,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TradingWrite);
        return await _businessPricing.CreatePolicyAsync(
            principal.BusinessProfileId,
            request,
            null,
            ct);
    }

    public async Task<BusinessPricingPolicyDto> UpdatePricingPolicyAsync(
        Guid policyId,
        UpdateBusinessPricingPolicyRequestDto request,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TradingWrite);
        return await _businessPricing.UpdatePolicyAsync(
            principal.BusinessProfileId,
            policyId,
            request,
            null,
            ct);
    }

    public async Task<BusinessPricingPolicyDto> DisablePricingPolicyAsync(
        Guid policyId,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TradingWrite);
        return await _businessPricing.DisablePolicyAsync(
            principal.BusinessProfileId,
            policyId,
            null,
            ct);
    }

    public async Task<IReadOnlyList<InstantPairDto>> GetInstantPairsAsync(
        Guid businessCustomerId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        await EnsureCustomerAsync(businessCustomerId, ct);
        return await _instant.GetPairsAsync(ct);
    }

    public async Task<InstantQuoteDto> CreateInstantQuoteAsync(
        Guid businessCustomerId,
        CreateInstantQuoteRequestDto request,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);

        return await _instant.CreateQuoteForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            customer.CountryCode,
            request,
            _context.GetRequiredPrincipal().BusinessProfileId,
            null,
            ct);
    }

    public async Task<InstantTradeDto> ExecuteInstantQuoteAsync(
        Guid businessCustomerId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);

        return await _instant.ExecuteQuoteForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            quoteId,
            null,
            ct);
    }

    public async Task<PagedResult<InstantTradeDto>> GetInstantTradesAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);

        return await _instant.GetTradesForOwnerAsync(
            FinancialAccountOwnerType.BusinessCustomer,
            customer.Id,
            page,
            pageSize,
            ct);
    }

    public async Task<BusinessTradingRfqDto> CreateRfqAsync(Guid businessCustomerId, CreateBusinessTradingRfqRequestDto request, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var requester = await EnsureCustomerAsync(businessCustomerId, ct);
        var counterparty = await EnsureCustomerAsync(request.CounterpartyBusinessCustomerId, ct);
        await EnsureMarketplaceCountryAccessAsync(requester.CountryCode, request.MarketplacePairId, ct);
        await EnsureMarketplaceCountryAccessAsync(counterparty.CountryCode, request.MarketplacePairId, ct);
        return await _rfqs.CreateAsync(FinancialAccountOwnerType.BusinessCustomer, requester.Id, FinancialAccountOwnerType.BusinessCustomer, counterparty.Id, null, request.MarketplacePairId, request.Side, request.Quantity, request.ExpiresAt, ct);
    }

    public async Task<BusinessTradingRfqDto> QuoteRfqAsync(Guid businessCustomerId, Guid rfqId, CreateBusinessTradingRfqQuoteRequestDto request, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _rfqs.QuoteAsync(FinancialAccountOwnerType.BusinessCustomer, customer.Id, null, rfqId, request.Price, request.ExpiresAt, ct);
    }

    public async Task<BusinessTradingRfqDto> AcceptRfqQuoteAsync(Guid businessCustomerId, Guid rfqId, Guid quoteId, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _rfqs.AcceptAsync(FinancialAccountOwnerType.BusinessCustomer, customer.Id, null, rfqId, quoteId, ct);
    }

    public async Task<BusinessTradingRfqDto> CancelRfqAsync(Guid businessCustomerId, Guid rfqId, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingWrite);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _rfqs.CancelAsync(FinancialAccountOwnerType.BusinessCustomer, customer.Id, null, rfqId, ct);
    }

    public async Task<BusinessTradingRfqDto> GetRfqAsync(Guid businessCustomerId, Guid rfqId, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _rfqs.GetAsync(FinancialAccountOwnerType.BusinessCustomer, customer.Id, rfqId, ct);
    }

    public async Task<PagedResult<BusinessTradingRfqDto>> GetRfqsAsync(Guid businessCustomerId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        RequireScope(EmbeddedFinanceScope.TradingRead);
        var customer = await EnsureCustomerAsync(businessCustomerId, ct);
        return await _rfqs.GetForOwnerAsync(FinancialAccountOwnerType.BusinessCustomer, customer.Id, page, pageSize, ct);
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope)
    {
        var principal = _context.GetRequiredPrincipal();
        if (!principal.HasScope(scope))
            throw new UnauthorizedAccessException(
                $"API application does not have required scope '{scope}'.");
        return principal;
    }

    private async Task<BusinessCustomer> EnsureCustomerAsync(
        Guid businessCustomerId,
        CancellationToken ct)
    {
        var principal = _context.GetRequiredPrincipal();

        return await _db.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == businessCustomerId &&
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.Status == BusinessCustomerStatus.Active &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Active business customer not found.");
    }

    private async Task EnsureMarketplaceCountryAccessAsync(
        string countryCode,
        Guid pairId,
        CancellationToken ct)
    {
        var pair = await _db.MarketplacePairs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pairId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Marketplace pair not found.");

        var mappings = await _db.CountryAssets
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == countryCode &&
                (x.AssetCode == pair.BaseAssetCode || x.AssetCode == pair.QuoteAssetCode))
            .ToListAsync(ct);

        if (!mappings.Any(x => x.AssetCode == pair.BaseAssetCode && x.CanTrade) ||
            !mappings.Any(x => x.AssetCode == pair.QuoteAssetCode && x.CanTrade))
            throw new InvalidOperationException(
                "Marketplace trading is not enabled for one or more assets in the business customer's country.");
    }
}
