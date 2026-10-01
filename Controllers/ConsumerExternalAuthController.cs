using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Auth;
using KorridorX.Infrastructure;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/auth/external")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(24576)]
[EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
public sealed class ConsumerExternalAuthController(IConsumerExternalAuthService service) : ControllerBase
{
    [AllowAnonymous, HttpGet("providers")]
    public IActionResult Providers() => Ok(ApiResponses.Ok(service.Providers()));

    [AllowAnonymous, HttpPost("challenge")]
    public IActionResult Challenge(ConsumerExternalChallengeRequestDto request) => Ok(ApiResponses.Ok(service.SignInChallenge(request.Provider)));

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(ConsumerExternalLoginRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await service.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
            return Ok(ApiResponses.Ok(result, result.Status == LoginStatuses.Authenticated ? "Login successful." : "Multi-factor authentication is required to complete sign-in."));
        }
        catch (ExternalIdentityProviderUnavailableException ex)
        { return StatusCode(503, ApiResponses.Fail(ex.Message, "EXTERNAL_PROVIDER_UNAVAILABLE")); }
    }

    [Authorize(Roles = "Consumer"), HttpGet("links")]
    public async Task<IActionResult> Links(CancellationToken ct) => Ok(ApiResponses.Ok(await service.LinksAsync(UserId(), ct)));

    [Authorize(Roles = "Consumer"), HttpPost("links/challenge")]
    public async Task<IActionResult> LinkChallenge(ConsumerExternalChallengeRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.LinkChallengeAsync(UserId(), request.Provider, ct)));

    [Authorize(Roles = "Consumer"), HttpPost("links")]
    public async Task<IActionResult> Link(ConsumerExternalLinkRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await service.LinkAsync(UserId(), request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
            return Ok(ApiResponses.Ok(result, "Provider linked. Sign in again to continue."));
        }
        catch (ExternalIdentityProviderUnavailableException ex)
        { return StatusCode(503, ApiResponses.Fail(ex.Message, "EXTERNAL_PROVIDER_UNAVAILABLE")); }
    }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
