using System.Security.Claims;
using KorridorX.Dtos.Customers;
using KorridorX.Infrastructure;
using KorridorX.Services.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/customer-profile")]
public class CustomerProfileController : ControllerBase
{
    private readonly ICustomerProfileService _service;

    public CustomerProfileController(ICustomerProfileService service)
    {
        _service = service;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = GetUserId();

        var response = await _service.GetMyProfileAsync(userId, ct);

        return Ok(ApiResponses.Ok(response, "Customer profile retrieved successfully."));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateCustomerProfileRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();

        var response = await _service.UpdateMyProfileAsync(userId, request, ct);

        return Ok(ApiResponses.Ok(response, "Customer profile updated successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid authenticated user.");

        return userId;
    }
}