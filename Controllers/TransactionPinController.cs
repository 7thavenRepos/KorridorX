using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Auth;
using KorridorX.Infrastructure;
using KorridorX.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/auth/transaction-pin")]
public sealed class TransactionPinController : ControllerBase
{
    private readonly ITransactionPinService _service;

    public TransactionPinController(ITransactionPinService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var result = await _service.GetStatusAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Transaction PIN status retrieved."));
    }

    [HttpPut]
    public async Task<IActionResult> Set(
        [FromBody] SetTransactionPinRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SetAsync(
            GetUserId(),
            request.CurrentPassword,
            request.Pin,
            ct);
        return Ok(ApiResponses.Ok(result, "Transaction PIN saved securely."));
    }

    [HttpPost("google/challenge")]
    public async Task<IActionResult> GoogleChallenge(CancellationToken ct)
    {
        var challenge = await _service.CreateGoogleChallengeAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(challenge, "Fresh Google verification requested."));
    }

    [HttpPut("google")]
    [RequestSizeLimit(24576)]
    public async Task<IActionResult> SetWithGoogle(
        [FromBody] SetGoogleTransactionPinRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SetWithGoogleAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Transaction PIN saved securely."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        return userId;
    }
}
