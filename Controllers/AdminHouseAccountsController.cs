using System.Security.Claims;
using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;
using KorridorX.Services.Treasury;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/treasury/house-accounts")]
public sealed class AdminHouseAccountsController(HouseAccountProvisioningService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? assetCode = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await service.GetAsync(assetCode, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "House accounts retrieved successfully."));
    }

    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] ProvisionHouseAccountsRequestDto request, CancellationToken ct)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var actorId) || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        return Ok(ApiResponses.Ok(await service.ProvisionAsync(actorId, request, ct),
            "House account provisioning completed. New accounts have zero balances; no funds were moved."));
    }
}
