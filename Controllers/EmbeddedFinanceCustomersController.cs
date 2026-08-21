using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;
[ApiController, Route("api/v1/embedded/customers")]
public sealed class EmbeddedFinanceCustomersController : ControllerBase
{
    private readonly IEmbeddedFinanceCustomerService _service;
    public EmbeddedFinanceCustomersController(IEmbeddedFinanceCustomerService service)=>_service=service;
    [HttpGet] public async Task<IActionResult> Customers([FromQuery]int page=1,[FromQuery]int pageSize=50,CancellationToken ct=default){var r=await _service.GetCustomersAsync(page,pageSize,ct);return Ok(ApiResponses.OkPaged(r.Items,r.Meta,"Business customers retrieved successfully."));}
    [HttpPost] public async Task<IActionResult> CreateCustomer([FromHeader(Name="Idempotency-Key")]string? key,[FromBody]CreateBusinessCustomerRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateCustomerAsync(request,key??"",ct),"Business customer created successfully."));
    [HttpGet("{businessCustomerId:guid}/accounts")] public async Task<IActionResult> Accounts(Guid businessCustomerId,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetAccountsAsync(businessCustomerId,ct),"Collection accounts retrieved successfully."));
    [HttpPost("{businessCustomerId:guid}/accounts")] public async Task<IActionResult> CreateAccount(Guid businessCustomerId,[FromHeader(Name="Idempotency-Key")]string? key,[FromBody]CreateCollectionAccountRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateAccountAsync(businessCustomerId,request,key??"",ct),"Collection account created successfully."));
}
