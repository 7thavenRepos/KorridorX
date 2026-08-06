using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/business/context")]
public class BusinessContextController : ControllerBase
{
    private readonly IBusinessAccessService _accessService;

    public BusinessContextController(IBusinessAccessService accessService)
    {
        _accessService = accessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailable(CancellationToken ct)
    {
        var result = await _accessService.GetAvailableBusinessesAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Available business contexts retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
