using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;
[ApiController, Route("api/v1/embedded/customers")]
public sealed class EmbeddedFinanceCustomersController : ControllerBase
{
    private readonly IEmbeddedFinanceCustomerService _service;
    private readonly ICollectionAccountProvisioningService _provisioning;
    private readonly IEmbeddedFinancePayoutService _payouts;
    private readonly IEmbeddedFinanceTransferService _transfers;
    private readonly IEmbeddedTradingService _trading;
    public EmbeddedFinanceCustomersController(
        IEmbeddedFinanceCustomerService service,
        ICollectionAccountProvisioningService provisioning,
        IEmbeddedFinancePayoutService payouts,
        IEmbeddedFinanceTransferService transfers,
        IEmbeddedTradingService trading)
    {
        _service = service;
        _provisioning = provisioning;
        _payouts = payouts;
        _transfers = transfers;
        _trading = trading;
    }
    [HttpGet] public async Task<IActionResult> Customers([FromQuery]int page=1,[FromQuery]int pageSize=50,CancellationToken ct=default){var r=await _service.GetCustomersAsync(page,pageSize,ct);return Ok(ApiResponses.OkPaged(r.Items,r.Meta,"Business customers retrieved successfully."));}
    [HttpPost] public async Task<IActionResult> CreateCustomer([FromHeader(Name="Idempotency-Key")]string? key,[FromBody]CreateBusinessCustomerRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateCustomerAsync(request,key??"",ct),"Business customer created successfully."));
    [HttpGet("{businessCustomerId:guid}/accounts")] public async Task<IActionResult> Accounts(Guid businessCustomerId,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetAccountsAsync(businessCustomerId,ct),"Collection accounts retrieved successfully."));
    [HttpPost("{businessCustomerId:guid}/accounts")] public async Task<IActionResult> CreateAccount(Guid businessCustomerId,[FromHeader(Name="Idempotency-Key")]string? key,[FromBody]CreateCollectionAccountRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateAccountAsync(businessCustomerId,request,key??"",ct),"Collection account created successfully."));
    [HttpGet("{businessCustomerId:guid}/accounts/{collectionAccountId:guid}/provider-mappings")]
    public async Task<IActionResult> ProviderMappings(Guid businessCustomerId, Guid collectionAccountId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _provisioning.GetMappingsAsync(businessCustomerId, collectionAccountId, ct),
            "Provider account mappings retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/accounts/{collectionAccountId:guid}/provision")]
    public async Task<IActionResult> Provision(
        Guid businessCustomerId,
        Guid collectionAccountId,
        [FromBody] ProvisionCollectionAccountRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _provisioning.ProvisionAsync(businessCustomerId, collectionAccountId, request, ct),
            "Provider collection account provisioned successfully."));

    [HttpGet("{businessCustomerId:guid}/payouts")]
    public async Task<IActionResult> Payouts(
        Guid businessCustomerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _payouts.GetPayoutsAsync(businessCustomerId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded payouts retrieved successfully."));
    }

    [HttpGet("{businessCustomerId:guid}/payouts/{payoutId:guid}")]
    public async Task<IActionResult> Payout(
        Guid businessCustomerId,
        Guid payoutId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _payouts.GetPayoutAsync(businessCustomerId, payoutId, ct),
            "Embedded payout retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/accounts/{collectionAccountId:guid}/payouts")]
    public async Task<IActionResult> CreatePayout(
        Guid businessCustomerId,
        Guid collectionAccountId,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        [FromBody] CreateEmbeddedPayoutRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _payouts.CreatePayoutAsync(
                businessCustomerId,
                collectionAccountId,
                request,
                key ?? "",
                ct),
            "Embedded payout created successfully."));


    [HttpPost("{businessCustomerId:guid}/accounts/{collectionAccountId:guid}/transfer-quotes")]
    public async Task<IActionResult> CreateTransferQuote(
        Guid businessCustomerId,
        Guid collectionAccountId,
        [FromBody] CreateEmbeddedTransferQuoteRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _transfers.CreateQuoteAsync(
                businessCustomerId,
                collectionAccountId,
                request,
                ct),
            "Embedded transfer quote created successfully."));

    [HttpGet("{businessCustomerId:guid}/transfer-quotes/{quoteId:guid}")]
    public async Task<IActionResult> TransferQuote(
        Guid businessCustomerId,
        Guid quoteId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _transfers.GetQuoteAsync(businessCustomerId, quoteId, ct),
            "Embedded transfer quote retrieved successfully."));

    [HttpGet("{businessCustomerId:guid}/transfers")]
    public async Task<IActionResult> Transfers(
        Guid businessCustomerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _transfers.GetTransfersAsync(
            businessCustomerId,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Embedded transfers retrieved successfully."));
    }

    [HttpGet("{businessCustomerId:guid}/transfers/{transferId:guid}")]
    public async Task<IActionResult> Transfer(
        Guid businessCustomerId,
        Guid transferId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _transfers.GetTransferAsync(
                businessCustomerId,
                transferId,
                ct),
            "Embedded transfer retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/accounts/{collectionAccountId:guid}/transfers")]
    public async Task<IActionResult> CreateTransfer(
        Guid businessCustomerId,
        Guid collectionAccountId,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        [FromBody] CreateEmbeddedTransferRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _transfers.CreateTransferAsync(
                businessCustomerId,
                collectionAccountId,
                request,
                key ?? "",
                ct),
            "Embedded transfer created successfully."));


    [HttpGet("{businessCustomerId:guid}/trading/balances")]
    public async Task<IActionResult> TradingBalances(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetBalancesAsync(businessCustomerId, ct),
            "Trading balances retrieved successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/marketplace/pairs")]
    public async Task<IActionResult> MarketplacePairs(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetMarketplacePairsAsync(businessCustomerId, ct),
            "Marketplace pairs retrieved successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/marketplace/pairs/{pairId:guid}/orderbook")]
    public async Task<IActionResult> MarketplaceOrderBook(
        Guid businessCustomerId,
        Guid pairId,
        [FromQuery] int depth = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _trading.GetOrderBookAsync(businessCustomerId, pairId, depth, ct),
            "Marketplace order book retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/marketplace/orders")]
    public async Task<IActionResult> CreateMarketplaceOrder(
        Guid businessCustomerId,
        [FromBody] CreateTradeOrderRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.CreateMarketplaceOrderAsync(businessCustomerId, request, ct),
            "Marketplace order created successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/marketplace/orders")]
    public async Task<IActionResult> MarketplaceOrders(
        Guid businessCustomerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _trading.GetMarketplaceOrdersAsync(businessCustomerId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace orders retrieved successfully."));
    }

    [HttpGet("{businessCustomerId:guid}/trading/marketplace/orders/{orderId:guid}")]
    public async Task<IActionResult> MarketplaceOrder(
        Guid businessCustomerId,
        Guid orderId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetMarketplaceOrderAsync(businessCustomerId, orderId, ct),
            "Marketplace order retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/marketplace/orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelMarketplaceOrder(
        Guid businessCustomerId,
        Guid orderId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.CancelMarketplaceOrderAsync(businessCustomerId, orderId, ct),
            "Marketplace order cancelled successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/marketplace/trades")]
    public async Task<IActionResult> MarketplaceTrades(
        Guid businessCustomerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _trading.GetMarketplaceTradesAsync(businessCustomerId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Marketplace trades retrieved successfully."));
    }

    [HttpGet("{businessCustomerId:guid}/trading/instant/pairs")]
    public async Task<IActionResult> InstantPairs(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetInstantPairsAsync(businessCustomerId, ct),
            "Instant pairs retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/instant/quotes")]
    public async Task<IActionResult> CreateInstantQuote(
        Guid businessCustomerId,
        [FromBody] CreateInstantQuoteRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.CreateInstantQuoteAsync(businessCustomerId, request, ct),
            "Instant quote created successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/instant/quotes/{quoteId:guid}/execute")]
    public async Task<IActionResult> ExecuteInstantQuote(
        Guid businessCustomerId,
        Guid quoteId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.ExecuteInstantQuoteAsync(businessCustomerId, quoteId, ct),
            "Instant quote executed successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/instant/trades")]
    public async Task<IActionResult> InstantTrades(
        Guid businessCustomerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _trading.GetInstantTradesAsync(businessCustomerId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Instant trades retrieved successfully."));
    }


    [HttpPost("{businessCustomerId:guid}/trading/rfqs")]
    public async Task<IActionResult> CreateTradingRfq(Guid businessCustomerId, [FromBody] CreateBusinessTradingRfqRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _trading.CreateRfqAsync(businessCustomerId, request, ct), "Trading RFQ created successfully."));

    [HttpGet("{businessCustomerId:guid}/trading/rfqs")]
    public async Task<IActionResult> TradingRfqs(Guid businessCustomerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _trading.GetRfqsAsync(businessCustomerId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Trading RFQs retrieved successfully."));
    }

    [HttpGet("{businessCustomerId:guid}/trading/rfqs/{rfqId:guid}")]
    public async Task<IActionResult> TradingRfq(Guid businessCustomerId, Guid rfqId, CancellationToken ct) => Ok(ApiResponses.Ok(await _trading.GetRfqAsync(businessCustomerId, rfqId, ct), "Trading RFQ retrieved successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/rfqs/{rfqId:guid}/quotes")]
    public async Task<IActionResult> QuoteTradingRfq(Guid businessCustomerId, Guid rfqId, [FromBody] CreateBusinessTradingRfqQuoteRequestDto request, CancellationToken ct) => Ok(ApiResponses.Ok(await _trading.QuoteRfqAsync(businessCustomerId, rfqId, request, ct), "Trading RFQ quote submitted successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/rfqs/{rfqId:guid}/quotes/{quoteId:guid}/accept")]
    public async Task<IActionResult> AcceptTradingRfqQuote(Guid businessCustomerId, Guid rfqId, Guid quoteId, CancellationToken ct) => Ok(ApiResponses.Ok(await _trading.AcceptRfqQuoteAsync(businessCustomerId, rfqId, quoteId, ct), "Trading RFQ quote accepted successfully."));

    [HttpPost("{businessCustomerId:guid}/trading/rfqs/{rfqId:guid}/cancel")]
    public async Task<IActionResult> CancelTradingRfq(Guid businessCustomerId, Guid rfqId, CancellationToken ct) => Ok(ApiResponses.Ok(await _trading.CancelRfqAsync(businessCustomerId, rfqId, ct), "Trading RFQ cancelled successfully."));

}
