using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/business/invitations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BusinessInvitationsController : ControllerBase
{
    private readonly IBusinessInvitationService _service;

    public BusinessInvitationsController(IBusinessInvitationService service)
    {
        _service = service;
    }

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpGet("{token}")]
    public async Task<IActionResult> GetPreview(string token, CancellationToken ct)
    {
        var result = await _service.GetPreviewAsync(token, ct);
        return Ok(ApiResponses.Ok(result, "Business invitation retrieved successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("Invalid authenticated user.");

        await _service.AcceptAsync(token, userId, email, saveChanges: true, ct: ct);
        return Ok(ApiResponses.Ok(new { accepted = true }, "Business invitation accepted successfully."));
    }
}
