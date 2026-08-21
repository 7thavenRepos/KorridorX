using KorridorX.Dtos.EmbeddedFinance;
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
    public EmbeddedFinanceCustomersController(
        IEmbeddedFinanceCustomerService service,
        ICollectionAccountProvisioningService provisioning,
        IEmbeddedFinancePayoutService payouts,
        IEmbeddedFinanceTransferService transfers)
    {
        _service = service;
        _provisioning = provisioning;
        _payouts = payouts;
        _transfers = transfers;
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

}
