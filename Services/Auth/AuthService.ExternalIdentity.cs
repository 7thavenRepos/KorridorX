using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Auth;

public partial class AuthService
{
    private const string SessionMethodProvider = "KorridorX.SessionAuthentication";

    private async Task<LoginResultDto> CompleteVerifiedLoginAsync(ApplicationUser user, LoginRequestDto request,
        string? ipAddress, string? userAgent, CancellationToken ct, string authenticationMethod = MfaSecurityPolicy.PasswordAuthenticationMethod)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var mfaRequired =
            (_mfaOptions.EnforceForPrivilegedRoles &&
             MfaSecurityPolicy.RequiresMfa(roles)) ||
            await _userManager.GetTwoFactorEnabledAsync(user);

        if (mfaRequired)
        {
            var enrolled = await _userManager.GetTwoFactorEnabledAsync(user);
            var purpose = enrolled
                ? MfaChallengePurposes.Verification
                : MfaChallengePurposes.Enrollment;

            if (!enrolled)
            {
                var resetKeyResult = await _userManager.ResetAuthenticatorKeyAsync(user);
                if (!resetKeyResult.Succeeded)
                    throw new InvalidOperationException("Authenticator enrollment could not be initialized.");
            }

            var challenge = await _mfaChallenges.CreateAsync(
                user.Id,
                purpose,
                ipAddress,
                userAgent,
                request.DeviceFingerprint,
                request.DeviceName,
                ct, authenticationMethod);

            _audit.Stage(new AuditRecordRequest(
                Action: "MFA_CHALLENGE_ISSUED",
                Category: "Authentication",
                EntityName: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                Metadata: new { Purpose = purpose },
                UserId: user.Id));

            await _db.SaveChangesAsync(ct);

            return new LoginResultDto(
                enrolled
                    ? LoginStatuses.MfaRequired
                    : LoginStatuses.MfaEnrollmentRequired,
                null,
                challenge);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await RecordLoginAsync(user.Id, request, ipAddress, userAgent, true, null, ct, saveImmediately: false);

        var refresh = _jwtTokenService.GenerateRefreshToken();
        var session = CreateRefreshToken(
            user.Id,
            refresh.Token,
            refresh.ExpiresAt,
            ipAddress,
            userAgent,
            request.DeviceFingerprint,
            request.DeviceName);
        _db.RefreshTokens.Add(session);
        StageExternalSessionMethod(user.Id, session.Id, authenticationMethod);

        await EnforceSessionLimitAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user, authenticationMethod: authenticationMethod);
        return new LoginResultDto(
            LoginStatuses.Authenticated,
            ToAuthResponse(user, access, refresh),
            null);
    }

    internal async Task<ApplicationUser> ExternalConsumerAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
            throw new UnauthorizedApiException("Account is not active.", "ACCOUNT_INACTIVE");
        ExternalIdentityPolicy.RequireConsumer(user.UserType, await _userManager.GetRolesAsync(user));
        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedApiException("The account is temporarily locked.", "ACCOUNT_LOCKED");
        if (_accountOptions.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            throw new UnauthorizedApiException("Email confirmation is required before sign-in.", "EMAIL_CONFIRMATION_REQUIRED");
        return user;
    }

    internal Task<LoginResultDto> CompleteExternalLoginAsync(ApplicationUser user, ConsumerExternalLoginRequestDto request,
        string? ipAddress, string? userAgent, CancellationToken ct) => CompleteVerifiedLoginAsync(user,
            new LoginRequestDto(user.Email ?? "", "", request.DeviceFingerprint, request.DeviceName), ipAddress, userAgent, ct,
            MfaSecurityPolicy.ExternalAuthenticationMethod);

    internal async Task VerifyExternalLinkPasswordAsync(ApplicationUser user, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || !await _userManager.CheckPasswordAsync(user, password))
        {
            await RecordExternalLinkFailureAsync(user.Id, ct);
            throw new UnauthorizedApiException("The current password and verification code could not be verified.", "EXTERNAL_LINK_REAUTHENTICATION_REQUIRED");
        }
    }

    internal async Task<bool> VerifyExternalLinkMfaAsync(ApplicationUser user, string? code, string? codeType, CancellationToken ct)
    {
        if (!await _userManager.GetTwoFactorEnabledAsync(user)) return true;
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(codeType)) return false;
        return (await VerifyMfaCodeAsync(user, code, codeType, ct)).Verified;
    }

    internal async Task RecordExternalLinkFailureAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is not null) await RecordFailedMfaManagementAsync(user, "EXTERNAL_LINK_REAUTHENTICATION_FAILED", ct);
    }

    internal async Task CompleteExternalLinkSecurityAsync(ApplicationUser user, string provider, string? ipAddress, CancellationToken ct)
    {
        var stamp = await _userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) throw new InvalidOperationException("Account security state could not be updated.");
        await RevokeAllActiveSessionsInternalAsync(user.Id, ipAddress, "External sign-in credential linked.", ct);
        await _userManager.ResetAccessFailedCountAsync(user);
        await QueueAccountSecurityNoticeAsync(user.Id, ("Sign-in method added to your KorridorX account",
            $"<p>A {provider} sign-in method was added to your account. Existing sessions were signed out. If you did not make this change, reset your password and contact support.</p>"), ct);
    }

    private void StageExternalSessionMethod(Guid userId, Guid sessionId, string method)
    {
        if (method != MfaSecurityPolicy.ExternalAuthenticationMethod) return;
        _db.UserTokens.Add(new IdentityUserToken<Guid> { UserId = userId,
            LoginProvider = SessionMethodProvider, Name = "Session:" + sessionId.ToString("N"), Value = method });
    }
}
