using System.Security.Claims;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize, ApiController, Route("api/business/embedded/customers")]
public sealed class BusinessCollectionAccountsController(BusinessCollectionAccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Customers([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await service.GetCustomersAsync(UserId(), page, pageSize, search, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Customers retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreatePortalCustomerRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.CreateCustomerAsync(UserId(), request, ct), "Customer saved successfully."));

    [HttpGet("{customerId:guid}/accounts")]
    public async Task<IActionResult> Accounts(Guid customerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetAccountsAsync(UserId(), customerId, ct), "Customer accounts retrieved successfully."));

    [HttpPost("{customerId:guid}/accounts")]
    public async Task<IActionResult> CreateAccount(Guid customerId, [FromBody] CreateCollectionAccountRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.CreateAccountAsync(UserId(), customerId, request, ct), "Account saved. Request bank details when its requirements are complete."));

    [HttpPost("{customerId:guid}/accounts/{accountId:guid}/provision")]
    public async Task<IActionResult> Provision(Guid customerId, Guid accountId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ProvisionAsync(UserId(), customerId, accountId, ct), "Bank account request status updated."));

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
