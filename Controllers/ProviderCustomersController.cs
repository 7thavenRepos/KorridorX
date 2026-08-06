using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/customer-profile/me/provider")]
public class ProviderCustomersController : ControllerBase
{
    private readonly IProviderCustomerService _service;

    public ProviderCustomersController(IProviderCustomerService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _service.GetMyProviderCustomerAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(
            result,
            "Provider customer retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
