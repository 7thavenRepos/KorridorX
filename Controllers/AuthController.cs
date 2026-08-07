using KorridorX.Configuration;
using KorridorX.Dtos.Auth;
using KorridorX.Infrastructure;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        var response = await _authService.RegisterAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return Ok(ApiResponses.Ok(response, "Registration successful."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);

        return Ok(ApiResponses.Ok(response, "Login successful."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequestDto request, CancellationToken ct)
    {
        var response = await _authService.RefreshTokenAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);

        return Ok(ApiResponses.Ok(response, "Token refreshed successfully."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var response = await _authService.GetCurrentUserAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(response, "Current user retrieved successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var response = await _authService.GetSessionsAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(response, "Sessions retrieved successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        await _authService.RevokeSessionAsync(
            GetUserId(),
            sessionId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            "Revoked by user.",
            ct);

        return Ok(ApiResponses.Ok(new { sessionId }, "Session revoked successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAllSessions(
        [FromBody] RevokeAllSessionsRequestDto request,
        CancellationToken ct)
    {
        var count = await _authService.RevokeAllSessionsAsync(
            GetUserId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(new { revokedSessions = count }, "All active sessions were revoked successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
