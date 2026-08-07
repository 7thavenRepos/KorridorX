using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly SessionSecurityOptions _sessionOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IJwtTokenService jwtTokenService,
        IOptions<SecurityOptions> securityOptions)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
        _sessionOptions = securityOptions.Value.Sessions;
    }

    public async Task<AuthResponseDto> RegisterAsync(
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

        var refresh = _jwtTokenService.GenerateRefreshToken();
        _db.RefreshTokens.Add(CreateRefreshToken(
            user.Id,
            refresh.Token,
            refresh.ExpiresAt,
            ipAddress,
            null,
            null,
            null));

        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);
        return ToAuthResponse(user, access, refresh);
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

        return new CurrentUserDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.CountryCode,
            user.UserType,
            user.Status);
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
