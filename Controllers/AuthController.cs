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
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("register/consumer")]
    public async Task<IActionResult> RegisterConsumer(
        ChannelRegistrationRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.RegisterConsumerAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return RegistrationResponse(response);
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("register/business")]
    public async Task<IActionResult> RegisterBusiness(
        ChannelRegistrationRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.RegisterBusinessAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return RegistrationResponse(response);
    }

    private IActionResult RegistrationResponse(RegistrationResultDto response)
    {
        var message = response.EmailConfirmationRequired
            ? "Registration successful. Check your email to confirm your account."
            : "Registration successful.";

        return Ok(ApiResponses.Ok(response, message));
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

        var message = response.Status switch
        {
            LoginStatuses.MfaEnrollmentRequired => "Authenticator enrollment is required to complete sign-in.",
            LoginStatuses.MfaRequired => "Multi-factor verification is required to complete sign-in.",
            _ => "Login successful."
        };

        return Ok(ApiResponses.Ok(response, message));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("mfa/enrollment/setup")]
    public async Task<IActionResult> GetMfaEnrollmentSetup(
        MfaEnrollmentSetupRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.GetMfaEnrollmentSetupAsync(request, ct);
        return Ok(ApiResponses.Ok(response, "Authenticator enrollment initialized."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("mfa/enrollment/confirm")]
    public async Task<IActionResult> ConfirmMfaEnrollment(
        MfaVerificationRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.ConfirmMfaEnrollmentAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);
        return Ok(ApiResponses.Ok(response, "Multi-factor authentication enabled."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("mfa/challenge/verify")]
    public async Task<IActionResult> VerifyMfaChallenge(
        MfaVerificationRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.VerifyMfaChallengeAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);
        return Ok(ApiResponses.Ok(response, "Multi-factor verification successful."));
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

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset(
        PasswordResetRequestDto request,
        CancellationToken ct)
    {
        await _authService.RequestPasswordResetAsync(request, ct);

        return Ok(ApiResponses.Ok(
            new AccountSecurityRequestResultDto(),
            "If the account can receive password-reset email, a secure link has been sent."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(
        PasswordResetConfirmationDto request,
        CancellationToken ct)
    {
        await _authService.ConfirmPasswordResetAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return Ok(ApiResponses.Ok(
            new AccountSecurityActionResultDto(),
            "Password reset successfully. Sign in with your new password."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("email-confirmation/request")]
    public async Task<IActionResult> RequestEmailConfirmation(
        EmailConfirmationRequestDto request,
        CancellationToken ct)
    {
        await _authService.RequestEmailConfirmationAsync(request, ct);

        return Ok(ApiResponses.Ok(
            new AccountSecurityRequestResultDto(),
            "If the account requires confirmation, a secure link has been sent."));
    }

    [EnableRateLimiting(SecurityRateLimitPolicies.Authentication)]
    [HttpPost("email-confirmation/confirm")]
    public async Task<IActionResult> ConfirmEmail(
        EmailConfirmationDto request,
        CancellationToken ct)
    {
        await _authService.ConfirmEmailAsync(request, ct);

        return Ok(ApiResponses.Ok(
            new AccountSecurityActionResultDto(),
            "Email address confirmed successfully. You can now sign in."));
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
    [HttpGet("mfa/status")]
    public async Task<IActionResult> GetMfaStatus(CancellationToken ct)
    {
        var response = await _authService.GetMfaStatusAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(response, "MFA status retrieved successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    [HttpPost("mfa/recovery-codes/regenerate")]
    public async Task<IActionResult> RegenerateMfaRecoveryCodes(
        MfaManagementVerificationDto request,
        CancellationToken ct)
    {
        var response = await _authService.RegenerateMfaRecoveryCodesAsync(
            GetUserId(),
            request,
            ct);
        return Ok(ApiResponses.Ok(response, "New recovery codes generated successfully."));
    }

    [Authorize]
    [EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
    [HttpPost("mfa/reset")]
    public async Task<IActionResult> ResetMfa(
        MfaManagementVerificationDto request,
        CancellationToken ct)
    {
        await _authService.ResetMfaAsync(
            GetUserId(),
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);
        return Ok(ApiResponses.Ok(
            new AccountSecurityActionResultDto(),
            "Multi-factor authentication reset. Sign in again to re-enroll."));
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
