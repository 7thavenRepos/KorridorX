using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
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

namespace KorridorX.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly SessionSecurityOptions _sessionOptions;
    private readonly AccountSecurityOptions _accountOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IJwtTokenService jwtTokenService,
        INotificationQueueService notifications,
        IAuditService audit,
        IOptions<SecurityOptions> securityOptions)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
        _notifications = notifications;
        _audit = audit;
        _sessionOptions = securityOptions.Value.Sessions;
        _accountOptions = securityOptions.Value.Accounts;
    }

    public async Task<RegistrationResultDto> RegisterAsync(
        RegisterRequestDto request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var roleName = RegistrationSecurityPolicy.ResolvePublicRole(request.UserType);
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
            UserType = request.UserType,
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

        if (request.UserType == UserType.Consumer)
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

        AuthResponseDto? authentication = null;
        if (!_accountOptions.RequireConfirmedEmail)
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
            authentication);
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        if (await _userManager.IsLockedOutAsync(user))
        {
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Account locked", ct);
            throw new UnauthorizedAccessException("The account is temporarily locked because of repeated failed login attempts.");
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Invalid password", ct);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (_accountOptions.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
        {
            await RecordLoginAsync(user.Id, request, ipAddress, userAgent, false, "Email not confirmed", ct);
            throw new UnauthorizedAccessException("Email confirmation is required before sign-in.");
        }

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
        return ToAuthResponse(user, access, refresh);
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
            throw new UnauthorizedAccessException("Account is not active.");
        if (_accountOptions.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            throw new UnauthorizedAccessException("Email confirmation is required before sign-in.");

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

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);
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
            user.EmailConfirmed);
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
