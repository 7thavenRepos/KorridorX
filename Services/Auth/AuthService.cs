using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Audit;
using KorridorX.Services.Notifications;
using KorridorX.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace KorridorX.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly IMfaChallengeStore _mfaChallenges;
    private readonly SessionSecurityOptions _sessionOptions;
    private readonly AccountSecurityOptions _accountOptions;
    private readonly MfaSecurityOptions _mfaOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IJwtTokenService jwtTokenService,
        INotificationQueueService notifications,
        IAuditService audit,
        IMfaChallengeStore mfaChallenges,
        IOptions<SecurityOptions> securityOptions)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
        _notifications = notifications;
        _audit = audit;
        _mfaChallenges = mfaChallenges;
        _sessionOptions = securityOptions.Value.Sessions;
        _accountOptions = securityOptions.Value.Accounts;
        _mfaOptions = securityOptions.Value.Mfa;
    }

    public Task<RegistrationResultDto> RegisterConsumerAsync(
        ChannelRegistrationRequestDto request,
        string? ipAddress,
        CancellationToken ct = default) =>
        RegisterAsync(request, UserType.Consumer, ipAddress, ct);

    public Task<RegistrationResultDto> RegisterBusinessAsync(
        ChannelRegistrationRequestDto request,
        string? ipAddress,
        CancellationToken ct = default) =>
        RegisterAsync(request, UserType.Business, ipAddress, ct);

    private async Task<RegistrationResultDto> RegisterAsync(
        ChannelRegistrationRequestDto request,
        UserType assignedUserType,
        string? ipAddress,
        CancellationToken ct)
    {
        var roleName = RegistrationSecurityPolicy.ResolvePublicRole(assignedUserType);
        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            throw new InvalidOperationException("Email address is already registered.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber,
            CountryCode = request.CountryCode,
            UserType = assignedUserType,
            Status = UserStatus.Active
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(x => x.Description));
            throw new InvalidOperationException(errors);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            var errors = string.Join("; ", roleResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errors)
                    ? "Unable to assign the account role."
                    : errors);
        }

        if (assignedUserType == UserType.Consumer)
        {
            _db.CustomerProfiles.Add(new CustomerProfile
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                CountryCode = request.CountryCode ?? "",
                CustomerType = CustomerType.Individual
            });
        }

        if (_accountOptions.RequireConfirmedEmail)
        {
            await QueueEmailConfirmationAsync(user, ct);
        }
        else
        {
            user.EmailConfirmed = true;
        }
        _audit.Stage(new AuditRecordRequest(
            Action: "ACCOUNT_REGISTERED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new
            {
                user.UserType,
                EmailConfirmationRequired = _accountOptions.RequireConfirmedEmail
            },
            UserId: user.Id));

        var requiresMfa =
            _mfaOptions.EnforceForPrivilegedRoles &&
            MfaSecurityPolicy.RequiresMfa([roleName]);
        AuthResponseDto? authentication = null;
        if (!_accountOptions.RequireConfirmedEmail && !requiresMfa)
        {
            var refresh = _jwtTokenService.GenerateRefreshToken();
            _db.RefreshTokens.Add(CreateRefreshToken(
                user.Id,
                refresh.Token,
                refresh.ExpiresAt,
                ipAddress,
                null,
                null,
                null));

            var access = await _jwtTokenService.GenerateAccessTokenAsync(user);
            authentication = ToAuthResponse(user, access, refresh);
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new RegistrationResultDto(
            user.Id,
            user.Email ?? email,
            _accountOptions.RequireConfirmedEmail,
            !_accountOptions.RequireConfirmedEmail && requiresMfa,
            authentication);
    }

    public async Task<LoginResultDto> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
            throw new UnauthorizedApiException("Invalid email or password.", "INVALID_CREDENTIALS");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedApiException("Account is not active.", "ACCOUNT_INACTIVE");

        if (await _userManager.IsLockedOutAsync(user))
        {
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Account locked", ct);
            throw new UnauthorizedApiException("The account is temporarily locked because of repeated failed login attempts.", "ACCOUNT_LOCKED");
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Invalid password", ct);
            throw new UnauthorizedApiException("Invalid email or password.", "INVALID_CREDENTIALS");
        }

        if (_accountOptions.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
        {
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Email not confirmed", ct);
            throw new UnauthorizedApiException("Email confirmation is required before sign-in.", "EMAIL_CONFIRMATION_REQUIRED");
        }

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
                ct);

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
        _db.RefreshTokens.Add(CreateRefreshToken(
            user.Id,
            refresh.Token,
            refresh.ExpiresAt,
            ipAddress,
            userAgent,
            request.DeviceFingerprint,
            request.DeviceName));

        await EnforceSessionLimitAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);
        return new LoginResultDto(
            LoginStatuses.Authenticated,
            ToAuthResponse(user, access, refresh),
            null);
    }

    public async Task<MfaEnrollmentSetupDto> GetMfaEnrollmentSetupAsync(
        MfaEnrollmentSetupRequestDto request,
        CancellationToken ct = default)
    {
        var challenge = await _mfaChallenges.ReadAsync(
            request,
            MfaChallengePurposes.Enrollment,
            ct);
        var user = await _userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active || user.TwoFactorEnabled)
            throw InvalidMfaChallenge();

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            throw InvalidMfaChallenge();

        var issuer = UrlEncoder.Default.Encode(_mfaOptions.Issuer);
        var accountName = UrlEncoder.Default.Encode(user.Email ?? user.UserName ?? user.Id.ToString());
        var authenticatorUri =
            $"otpauth://totp/{issuer}:{accountName}?secret={key}&issuer={issuer}&digits=6&period=30";

        return new MfaEnrollmentSetupDto(
            FormatAuthenticatorKey(key),
            authenticatorUri);
    }

    public async Task<MfaCompletionDto> ConfirmMfaEnrollmentAsync(
        MfaVerificationRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var redemption = await _mfaChallenges.AdvanceAsync(
            request,
            MfaChallengePurposes.Enrollment,
            ct);
        var user = await _userManager.FindByIdAsync(redemption.Challenge.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active || user.TwoFactorEnabled)
            throw InvalidMfaChallenge();
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _mfaChallenges.ConsumeAsync(redemption, ct);
            throw new UnauthorizedAccessException("The account is temporarily locked because of repeated failed verification attempts.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var verification = await VerifyMfaCodeAsync(
            user,
            request.Code,
            MfaCodeTypes.Authenticator,
            ct);
        if (!verification.Verified)
        {
            await transaction.RollbackAsync(ct);
            await RecordFailedMfaAttemptAsync(user, redemption, "Invalid enrollment code", ct);
            throw InvalidMfaCode();
        }

        var enabledResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabledResult.Succeeded)
            throw new InvalidOperationException("Multi-factor authentication could not be enabled.");

        var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
                user,
                _mfaOptions.RecoveryCodeCount))?
            .ToArray() ?? [];
        if (recoveryCodes.Length < Math.Min(8, _mfaOptions.RecoveryCodeCount))
            throw new InvalidOperationException("Recovery codes could not be generated.");

        var enrolledAt = DateTime.UtcNow;
        await _mfaChallenges.SetEnrollmentEpochAsync(user.Id, enrolledAt, ct);
        var revokedSessions = await RevokeAllActiveSessionsInternalAsync(
            user.Id,
            ipAddress,
            "MFA enrollment completed.",
            ct);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            throw new InvalidOperationException("MFA was enabled, but account security state could not be updated.");

        if (!await _mfaChallenges.ConsumeAsync(redemption, ct))
            throw InvalidMfaChallenge();

        await _userManager.ResetAccessFailedCountAsync(user);
        AddLoginHistory(user.Id, redemption.Challenge, true, null);
        _audit.Stage(new AuditRecordRequest(
            Action: "MFA_ENROLLED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new { MfaEnabled = true },
            Metadata: new
            {
                RecoveryCodesIssued = recoveryCodes.Length,
                RevokedSessions = revokedSessions
            },
            UserId: user.Id));

        var notice = AccountSecurityEmailFactory.CreateMfaEnabled(user.FirstName);
        await QueueAccountSecurityNoticeAsync(user.Id, notice, ct);

        var authentication = await StageAuthenticatedSessionAsync(
            user,
            redemption.Challenge,
            ipAddress,
            userAgent,
            ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new MfaCompletionDto(authentication, recoveryCodes);
    }

    public async Task<MfaCompletionDto> VerifyMfaChallengeAsync(
        MfaVerificationRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var redemption = await _mfaChallenges.AdvanceAsync(
            request,
            MfaChallengePurposes.Verification,
            ct);
        var user = await _userManager.FindByIdAsync(redemption.Challenge.UserId.ToString());
        if (user is null ||
            user.Status != UserStatus.Active ||
            !await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw InvalidMfaChallenge();
        }
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _mfaChallenges.ConsumeAsync(redemption, ct);
            throw new UnauthorizedAccessException("The account is temporarily locked because of repeated failed verification attempts.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var verification = await VerifyMfaCodeAsync(user, request.Code, request.CodeType, ct);
        if (!verification.Verified)
        {
            await transaction.RollbackAsync(ct);
            await RecordFailedMfaAttemptAsync(user, redemption, "Invalid MFA code", ct);
             throw InvalidMfaCode();
        }

        var enrollmentEpoch = await _mfaChallenges.GetEnrollmentEpochAsync(user.Id, ct);
        var revokedSessions = 0;
        if (enrollmentEpoch is null)
        {
            await _mfaChallenges.SetEnrollmentEpochAsync(user.Id, DateTime.UtcNow, ct);
            revokedSessions = await RevokeAllActiveSessionsInternalAsync(
                user.Id,
                ipAddress,
                "MFA session epoch initialized.",
                ct);
        }

        if (!await _mfaChallenges.ConsumeAsync(redemption, ct))
            throw InvalidMfaChallenge();

        await _userManager.ResetAccessFailedCountAsync(user);
        AddLoginHistory(user.Id, redemption.Challenge, true, null);
        _audit.Stage(new AuditRecordRequest(
            Action: "MFA_VERIFIED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Metadata: new
            {
                CodeType = verification.CodeType,
                RevokedSessions = revokedSessions
            },
            UserId: user.Id));

        if (verification.RecoveryCodeUsed)
        {
            var remaining = await _userManager.CountRecoveryCodesAsync(user);
            _audit.Stage(new AuditRecordRequest(
                Action: "MFA_RECOVERY_CODE_USED",
                Category: "Authentication",
                EntityName: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                Metadata: new { RecoveryCodesRemaining = remaining },
                UserId: user.Id));
        }

        var authentication = await StageAuthenticatedSessionAsync(
            user,
            redemption.Challenge,
            ipAddress,
            userAgent,
            ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new MfaCompletionDto(authentication, []);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var tokenHash = RefreshTokenSecurity.Hash(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == tokenHash || x.Token == request.RefreshToken, ct);

        if (existing is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (existing.IsRevoked)
        {
            if (!string.IsNullOrWhiteSpace(existing.ReplacedByToken))
            {
                await RevokeAllActiveSessionsInternalAsync(
                    existing.UserId,
                    ipAddress,
                    "Refresh token reuse detected.",
                    ct);
                await _db.SaveChangesAsync(ct);
            }

            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");
        }

        if (!existing.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        var user = existing.User;
        if (user.Status != UserStatus.Active)
            throw new UnauthorizedApiException("Account is not active.", "ACCOUNT_INACTIVE");
        if (_accountOptions.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            throw new UnauthorizedApiException("Email confirmation is required before sign-in.", "EMAIL_CONFIRMATION_REQUIRED");

        var roles = await _userManager.GetRolesAsync(user);
        var mfaRequired =
            (_mfaOptions.EnforceForPrivilegedRoles &&
             MfaSecurityPolicy.RequiresMfa(roles)) ||
            await _userManager.GetTwoFactorEnabledAsync(user);
        if (mfaRequired)
        {
            var enrollmentEpoch = await _mfaChallenges.GetEnrollmentEpochAsync(user.Id, ct);
            if (!user.TwoFactorEnabled ||
                enrollmentEpoch is null ||
                existing.CreatedAt < enrollmentEpoch.Value)
            {
                Revoke(existing, ipAddress, "MFA reauthentication required.");
                await _db.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Multi-factor authentication is required before this session can be refreshed.");
            }
        }

        var replacement = _jwtTokenService.GenerateRefreshToken();
        var replacementHash = RefreshTokenSecurity.Hash(replacement.Token);
        var now = DateTime.UtcNow;

        existing.IsRevoked = true;
        existing.RevokedAt = now;
        existing.RevokedByIp = ipAddress;
        existing.RevokedReason = "Rotated";
        existing.ReplacedByToken = replacementHash;
        existing.LastUsedAt = now;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = replacementHash,
            ExpiresAt = replacement.ExpiresAt,
            CreatedByIp = ipAddress,
            UserAgent = Clean(userAgent, 1000),
            DeviceFingerprint = Clean(request.DeviceFingerprint ?? existing.DeviceFingerprint, 250),
            DeviceName = Clean(request.DeviceName ?? existing.DeviceName, 250),
            LastUsedAt = now
        });

        await EnforceSessionLimitAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user, mfaRequired);
        return ToAuthResponse(user, access, replacement);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var roles = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId && role.Name != null
                orderby role.Name
                select role.Name!)
            .ToListAsync(ct);

        var mfaRequired =
            _mfaOptions.EnforceForPrivilegedRoles &&
            MfaSecurityPolicy.RequiresMfa(roles);

        return new CurrentUserDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.CountryCode,
            user.UserType,
            user.Status,
            roles,
            user.EmailConfirmed,
            mfaRequired,
            user.TwoFactorEnabled);
    }

    public async Task<MfaStatusDto> GetMfaStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);
        var required =
            _mfaOptions.EnforceForPrivilegedRoles &&
            MfaSecurityPolicy.RequiresMfa(roles);
        var enabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var recoveryCodesRemaining = enabled
            ? await _userManager.CountRecoveryCodesAsync(user)
            : 0;

        return new MfaStatusDto(required, enabled, recoveryCodesRemaining);
    }

    public async Task<MfaRecoveryCodesDto> RegenerateMfaRecoveryCodesAsync(
        Guid userId,
        MfaManagementVerificationDto request,
        CancellationToken ct = default)
    {
        var user = await GetActiveMfaUserAsync(userId);
        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            await RecordFailedMfaManagementAsync(user, "MFA_RECOVERY_CODES_REGENERATION_FAILED", ct);
            throw InvalidMfaReauthentication();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var verification = await VerifyMfaCodeAsync(user, request.Code, request.CodeType, ct);
        if (!verification.Verified)
        {
            await transaction.RollbackAsync(ct);
            await RecordFailedMfaManagementAsync(user, "MFA_RECOVERY_CODES_REGENERATION_FAILED", ct);
            throw InvalidMfaReauthentication();
        }

        var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
                user,
                _mfaOptions.RecoveryCodeCount))?
            .ToArray() ?? [];
        if (recoveryCodes.Length < Math.Min(8, _mfaOptions.RecoveryCodeCount))
            throw new InvalidOperationException("Recovery codes could not be generated.");

        await _userManager.ResetAccessFailedCountAsync(user);
        _audit.Stage(new AuditRecordRequest(
            Action: "MFA_RECOVERY_CODES_REGENERATED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Metadata: new
            {
                CodeType = verification.CodeType,
                RecoveryCodesIssued = recoveryCodes.Length
            },
            UserId: user.Id));

        var notice = AccountSecurityEmailFactory.CreateMfaRecoveryCodesRegenerated(user.FirstName);
        await QueueAccountSecurityNoticeAsync(user.Id, notice, ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new MfaRecoveryCodesDto(recoveryCodes);
    }

    public async Task ResetMfaAsync(
        Guid userId,
        MfaManagementVerificationDto request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var user = await GetActiveMfaUserAsync(userId);
        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            await RecordFailedMfaManagementAsync(user, "MFA_RESET_FAILED", ct);
            throw InvalidMfaReauthentication();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var verification = await VerifyMfaCodeAsync(user, request.Code, request.CodeType, ct);
        if (!verification.Verified)
        {
            await transaction.RollbackAsync(ct);
            await RecordFailedMfaManagementAsync(user, "MFA_RESET_FAILED", ct);
            throw InvalidMfaReauthentication();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var disabledResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabledResult.Succeeded)
            throw new InvalidOperationException("Multi-factor authentication could not be reset.");

        var resetKeyResult = await _userManager.ResetAuthenticatorKeyAsync(user);
        if (!resetKeyResult.Succeeded)
            throw new InvalidOperationException("The authenticator key could not be reset.");

        await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
        await _mfaChallenges.ClearAsync(user.Id, ct);
        var revokedSessions = await RevokeAllActiveSessionsInternalAsync(
            user.Id,
            ipAddress,
            "MFA reset completed.",
            ct);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            throw new InvalidOperationException("MFA was reset, but account security state could not be updated.");

        _audit.Stage(new AuditRecordRequest(
            Action: "MFA_RESET",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            OldValues: new { MfaEnabled = true },
            NewValues: new { MfaEnabled = false },
            Metadata: new
            {
                CodeType = verification.CodeType,
                RevokedSessions = revokedSessions
            },
            UserId: user.Id));

        var notice = AccountSecurityEmailFactory.CreateMfaReset(user.FirstName);
        await QueueAccountSecurityNoticeAsync(user.Id, notice, ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<UserSessionDto>> GetSessionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _db.RefreshTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
            .Take(100)
            .Select(x => new UserSessionDto(
                x.Id,
                x.DeviceName,
                x.DeviceFingerprint,
                x.CreatedByIp,
                x.UserAgent,
                x.CreatedAt,
                x.ExpiresAt,
                x.LastUsedAt,
                !x.IsRevoked && DateTime.UtcNow < x.ExpiresAt,
                x.RevokedAt,
                x.RevokedReason))
            .ToListAsync(ct);
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? ipAddress,
        string? reason,
        CancellationToken ct = default)
    {
        var session = await _db.RefreshTokens.FirstOrDefaultAsync(x =>
            x.Id == sessionId && x.UserId == userId,
            ct) ?? throw new InvalidOperationException("Session not found.");

        if (!session.IsRevoked)
        {
            Revoke(session, ipAddress, reason ?? "Revoked by user.");
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> RevokeAllSessionsAsync(
        Guid userId,
        string? ipAddress,
        string? reason,
        CancellationToken ct = default)
    {
        var count = await RevokeAllActiveSessionsInternalAsync(
            userId,
            ipAddress,
            reason ?? "All sessions revoked by user.",
            ct);
        await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task RequestPasswordResetAsync(
        PasswordResetRequestDto request,
        CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null || user.Status != UserStatus.Active)
                return;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var message = AccountSecurityEmailFactory.CreatePasswordReset(
                _accountOptions.FrontendBaseUrl,
                user.Id,
                IdentityTokenCodec.Encode(token),
                user.FirstName);

            await _notifications.QueueUserAsync(
                user.Id,
                message.Subject,
                message.Body,
                NotificationSecurityPolicy.AccountSecurityEntityType,
                user.Id,
                ct);

            _audit.Stage(new AuditRecordRequest(
                Action: "PASSWORD_RESET_REQUESTED",
                Category: "Authentication",
                EntityName: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                UserId: user.Id));

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            await ApplyEnumerationSafeDelayAsync(startedAt, ct);
        }
    }

    public async Task ConfirmPasswordResetAsync(
        PasswordResetConfirmationDto request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty ||
            !IdentityTokenCodec.TryDecode(request.Token, out var token))
        {
            throw InvalidPasswordReset();
        }

        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active)
            throw InvalidPasswordReset();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(x => string.Equals(x.Code, "InvalidToken", StringComparison.Ordinal)))
                throw InvalidPasswordReset();

            var errors = string.Join("; ", result.Errors.Select(x => x.Description));
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errors)
                    ? "The new password does not meet account security requirements."
                    : errors);
        }

        var revokedSessions = await RevokeAllActiveSessionsInternalAsync(
            user.Id,
            ipAddress,
            "Password changed.",
            ct);

        _audit.Stage(new AuditRecordRequest(
            Action: "PASSWORD_RESET_COMPLETED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Metadata: new { RevokedSessions = revokedSessions },
            UserId: user.Id));

        var changedMessage = AccountSecurityEmailFactory.CreatePasswordChanged(user.FirstName);
        await _notifications.QueueUserAsync(
            user.Id,
            changedMessage.Subject,
            changedMessage.Body,
            "AccountSecurityNotice",
            user.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RequestEmailConfirmationAsync(
        EmailConfirmationRequestDto request,
        CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null ||
                user.Status != UserStatus.Active ||
                await _userManager.IsEmailConfirmedAsync(user))
            {
                return;
            }

            await QueueEmailConfirmationAsync(user, ct);
            _audit.Stage(new AuditRecordRequest(
                Action: "EMAIL_CONFIRMATION_REQUESTED",
                Category: "Authentication",
                EntityName: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                UserId: user.Id));

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            await ApplyEnumerationSafeDelayAsync(startedAt, ct);
        }
    }

    public async Task ConfirmEmailAsync(
        EmailConfirmationDto request,
        CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty ||
            !IdentityTokenCodec.TryDecode(request.Token, out var token))
        {
            throw InvalidEmailConfirmation();
        }

        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active)
            throw InvalidEmailConfirmation();

        if (await _userManager.IsEmailConfirmedAsync(user))
            throw InvalidEmailConfirmation();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            throw InvalidEmailConfirmation();

        var securityStampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!securityStampResult.Succeeded)
            throw new InvalidOperationException("Email was confirmed, but account security state could not be updated.");

        _audit.Stage(new AuditRecordRequest(
            Action: "EMAIL_CONFIRMED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new { EmailConfirmed = true },
            UserId: user.Id));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<MfaCodeVerificationResult> VerifyMfaCodeAsync(
        ApplicationUser user,
        string code,
        string codeType,
        CancellationToken ct)
    {
        if (string.Equals(codeType, MfaCodeTypes.Authenticator, StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCode = NormalizeAuthenticatorCode(code);
            if (normalizedCode.Length != 6 || normalizedCode.Any(x => !char.IsAsciiDigit(x)))
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.Authenticator);

            var verified = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                normalizedCode);
            if (!verified)
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.Authenticator);

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(authenticatorKey) ||
                !await _mfaChallenges.TryClaimCodeAsync(
                    user.Id,
                    authenticatorKey,
                    normalizedCode,
                    ct))
            {
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.Authenticator);
            }

            return MfaCodeVerificationResult.Succeeded(
                MfaCodeTypes.Authenticator,
                recoveryCodeUsed: false);
        }

        if (string.Equals(codeType, MfaCodeTypes.RecoveryCode, StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCode = code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(normalizedCode))
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.RecoveryCode);

            var result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, normalizedCode);
            if (!result.Succeeded)
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.RecoveryCode);

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(authenticatorKey) ||
                !await _mfaChallenges.TryClaimCodeAsync(
                    user.Id,
                    authenticatorKey,
                    normalizedCode,
                    ct))
            {
                return MfaCodeVerificationResult.Failed(MfaCodeTypes.RecoveryCode);
            }

            return MfaCodeVerificationResult.Succeeded(
                MfaCodeTypes.RecoveryCode,
                recoveryCodeUsed: true);
        }

        return MfaCodeVerificationResult.Failed("Unknown");
    }

    private async Task<AuthResponseDto> StageAuthenticatedSessionAsync(
        ApplicationUser user,
        StoredMfaChallenge challenge,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var refresh = _jwtTokenService.GenerateRefreshToken();
        _db.RefreshTokens.Add(CreateRefreshToken(
            user.Id,
            refresh.Token,
            refresh.ExpiresAt,
            ipAddress ?? challenge.IpAddress,
            userAgent ?? challenge.UserAgent,
            challenge.DeviceFingerprint,
            challenge.DeviceName));

        await EnforceSessionLimitAsync(user.Id, ct);
        var access = await _jwtTokenService.GenerateAccessTokenAsync(user, mfaAuthenticated: true);
        return ToAuthResponse(user, access, refresh);
    }

    private async Task RecordFailedMfaAttemptAsync(
        ApplicationUser user,
        MfaChallengeRedemption redemption,
        string failureReason,
        CancellationToken ct)
    {
        await _userManager.AccessFailedAsync(user);
        if (redemption.Challenge.AttemptCount >= redemption.Challenge.MaximumAttempts)
            await _mfaChallenges.ConsumeAsync(redemption, ct);

        AddLoginHistory(user.Id, redemption.Challenge, false, failureReason);
        _audit.Stage(new AuditRecordRequest(
            Action: "MFA_CHALLENGE_FAILED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Metadata: new
            {
                redemption.Challenge.Purpose,
                AttemptsRemaining = Math.Max(
                    0,
                    redemption.Challenge.MaximumAttempts - redemption.Challenge.AttemptCount)
            },
            UserId: user.Id));
        await _db.SaveChangesAsync(ct);
    }

    private async Task RecordFailedMfaManagementAsync(
        ApplicationUser user,
        string action,
        CancellationToken ct)
    {
        await _userManager.AccessFailedAsync(user);
        _audit.Stage(new AuditRecordRequest(
            Action: action,
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            UserId: user.Id));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ApplicationUser> GetActiveMfaUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            user.Status != UserStatus.Active ||
            !await _userManager.GetTwoFactorEnabledAsync(user) ||
            await _userManager.IsLockedOutAsync(user))
        {
            throw new InvalidOperationException("Multi-factor authentication is not enabled.");
        }

        return user;
    }

    private void AddLoginHistory(
        Guid userId,
        StoredMfaChallenge challenge,
        bool successful,
        string? failureReason)
    {
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            IpAddress = Clean(challenge.IpAddress, 100),
            UserAgent = Clean(challenge.UserAgent, 1000),
            DeviceFingerprint = Clean(challenge.DeviceFingerprint, 250),
            DeviceName = Clean(challenge.DeviceName, 250),
            WasSuccessful = successful,
            FailureReason = failureReason
        });
    }

    private Task QueueAccountSecurityNoticeAsync(
        Guid userId,
        (string Subject, string Body) notice,
        CancellationToken ct) =>
        _notifications.QueueUserAsync(
            userId,
            notice.Subject,
            notice.Body,
            NotificationSecurityPolicy.AccountSecurityEntityType,
            userId,
            ct);

    private static string NormalizeAuthenticatorCode(string code) =>
        code.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FormatAuthenticatorKey(string key) =>
        string.Join(
            " ",
            key.ToUpperInvariant()
                .Chunk(4)
                .Select(chunk => new string(chunk)));

    private static UnauthorizedAccessException InvalidMfaChallenge() =>
        new("The MFA challenge is invalid, expired, or has already been used.");

    private static UnauthorizedAccessException InvalidMfaCode() =>
        new("The verification code is invalid, expired, or has already been used.");

    private static UnauthorizedAccessException InvalidMfaReauthentication() =>
        new("The current password and verification code could not be verified.");

    private async Task QueueEmailConfirmationAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var message = AccountSecurityEmailFactory.CreateEmailConfirmation(
            _accountOptions.FrontendBaseUrl,
            user.Id,
            IdentityTokenCodec.Encode(token),
            user.FirstName);

        await _notifications.QueueUserAsync(
            user.Id,
            message.Subject,
            message.Body,
            NotificationSecurityPolicy.AccountSecurityEntityType,
            user.Id,
            ct);
    }

    private static InvalidOperationException InvalidPasswordReset() =>
        new("The password-reset request is invalid or has expired.");

    private static InvalidOperationException InvalidEmailConfirmation() =>
        new("The email-confirmation request is invalid or has expired.");

    private sealed record MfaCodeVerificationResult(
        bool Verified,
        string CodeType,
        bool RecoveryCodeUsed)
    {
        public static MfaCodeVerificationResult Failed(string codeType) =>
            new(false, codeType, false);

        public static MfaCodeVerificationResult Succeeded(
            string codeType,
            bool recoveryCodeUsed) =>
            new(true, codeType, recoveryCodeUsed);
    }

    private static async Task ApplyEnumerationSafeDelayAsync(
        long startedAt,
        CancellationToken ct)
    {
        var remaining = TimeSpan.FromMilliseconds(250) - Stopwatch.GetElapsedTime(startedAt);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, ct);
    }

    private async Task RecordLoginAsync(
        Guid userId,
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        bool successful,
        string? failureReason,
        CancellationToken ct,
        bool saveImmediately = true)
    {
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            IpAddress = Clean(ipAddress, 100),
            UserAgent = Clean(userAgent, 1000),
            DeviceFingerprint = Clean(request.DeviceFingerprint, 250),
            DeviceName = Clean(request.DeviceName, 250),
            WasSuccessful = successful,
            FailureReason = failureReason
        });

        if (saveImmediately)
            await _db.SaveChangesAsync(ct);
    }

    private async Task EnforceSessionLimitAsync(Guid userId, CancellationToken ct)
    {
        var maximum = Math.Max(1, _sessionOptions.MaximumActiveSessionsPerUser);
        var active = await _db.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
            .ToListAsync(ct);

        foreach (var session in active.Skip(Math.Max(0, maximum - 1)))
            Revoke(session, null, "Maximum active session limit exceeded.");
    }

    private async Task<int> RevokeAllActiveSessionsInternalAsync(
        Guid userId,
        string? ipAddress,
        string reason,
        CancellationToken ct)
    {
        var sessions = await _db.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked && x.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var session in sessions)
            Revoke(session, ipAddress, reason);

        return sessions.Count;
    }

    private static void Revoke(RefreshToken session, string? ipAddress, string reason)
    {
        session.IsRevoked = true;
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedByIp = Clean(ipAddress, 100);
        session.RevokedReason = Clean(reason, 500);
        session.LastUpdatedAt = DateTime.UtcNow;
    }

    private static RefreshToken CreateRefreshToken(
        Guid userId,
        string rawToken,
        DateTime expiresAt,
        string? ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        string? deviceName) =>
        new()
        {
            UserId = userId,
            Token = RefreshTokenSecurity.Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedByIp = Clean(ipAddress, 100),
            UserAgent = Clean(userAgent, 1000),
            DeviceFingerprint = Clean(deviceFingerprint, 250),
            DeviceName = Clean(deviceName, 250),
            LastUsedAt = DateTime.UtcNow
        };

    private static AuthResponseDto ToAuthResponse(
        ApplicationUser user,
        (string Token, DateTime ExpiresAt) access,
        (string Token, DateTime ExpiresAt) refresh) =>
        new(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            access.Token,
            refresh.Token,
            access.ExpiresAt,
            refresh.ExpiresAt);

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
