using KorridorX.Dtos.Auth;
using KorridorX.Infrastructure;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _authService.RegisterAsync(request, ipAddress, ct);

        return Ok(ApiResponses.Ok(response, "Registration successful."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var response = await _authService.LoginAsync(request, ipAddress, userAgent, ct);

        return Ok(ApiResponses.Ok(response, "Login successful."));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequestDto request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _authService.RefreshTokenAsync(request, ipAddress, ct);

        return Ok(ApiResponses.Ok(response, "Token refreshed successfully."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(ApiResponses.Fail("Invalid authenticated user.", "INVALID_USER"));
        }

        var response = await _authService.GetCurrentUserAsync(userId, ct);

        return Ok(ApiResponses.Ok(response, "Current user retrieved successfully."));
    }
}