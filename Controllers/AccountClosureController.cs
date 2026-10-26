using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.AccountClosure;
using KorridorX.Infrastructure;
using KorridorX.Services.AccountClosure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

// Request/withdraw only: these endpoints never delete an account or move funds.
[ApiController]
[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/account-closure")]
public sealed class AccountClosureController(IAccountClosureService service) : ControllerBase
{
    [Authorize(Roles = "Consumer")]
    [HttpGet("personal")]
    public async Task<IActionResult> Personal(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetPersonalAsync(UserId(), ct)));

    [Authorize(Roles = "Consumer")]
    [HttpPost("personal/request")]
    public async Task<IActionResult> RequestPersonal([FromBody] AccountClosureRequestInput request, CancellationToken ct)
    {
        if (request is null || !request.Confirm)
            return BadRequest(ApiResponses.Fail("Confirm that you wish to request account closure."));
        return Accepted(ApiResponses.Ok(await service.RequestPersonalAsync(UserId(), ct),
            "Your account closure request has been received. Your account remains active during review."));
    }

    [Authorize(Roles = "Consumer")]
    [HttpPost("personal/withdraw-request")]
    public async Task<IActionResult> WithdrawPersonal(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.WithdrawPersonalAsync(UserId(), ct),
            "Your account closure request has been withdrawn."));

    [Authorize(Roles = "Business,BusinessAdmin")]
    [HttpGet("business")]
    public async Task<IActionResult> Business(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetBusinessAsync(UserId(), ct)));

    [Authorize(Roles = "Business,BusinessAdmin")]
    [HttpPost("business/request")]
    public async Task<IActionResult> RequestBusiness([FromBody] AccountClosureRequestInput request, CancellationToken ct)
    {
        if (request is null || !request.Confirm)
            return BadRequest(ApiResponses.Fail("Confirm that you wish to request business account closure."));
        return Accepted(ApiResponses.Ok(await service.RequestBusinessAsync(UserId(), ct),
            "The business closure request has been received. The business remains active during review."));
    }

    [Authorize(Roles = "Business,BusinessAdmin")]
    [HttpPost("business/withdraw-request")]
    public async Task<IActionResult> WithdrawBusiness(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.WithdrawBusinessAsync(UserId(), ct),
            "The business closure request has been withdrawn."));

    [Authorize(Roles = "Admin,SuperAdmin,Operations")]
    [HttpGet("admin/requests")]
    public async Task<IActionResult> AdminQueue(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await service.ListForOperationsAsync(page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta));
    }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
