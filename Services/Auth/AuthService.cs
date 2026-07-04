using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        string? ipAddress,
        CancellationToken ct = default)
    {
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

        var roleName = request.UserType switch
        {
            UserType.Consumer => "Consumer",
            UserType.Business => "Business",
            UserType.Admin => "Admin",
            _ => "Consumer"
        };

        await _userManager.AddToRoleAsync(user, roleName);

        if (request.UserType == UserType.Consumer)
        {
            var profile = new CustomerProfile
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                CountryCode = request.CountryCode ?? "",
                CustomerType = CustomerType.Individual
            };

            _db.CustomerProfiles.Add(profile);
        }

        var refresh = _jwtTokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh.Token,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);

        return new AuthResponseDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            access.Token,
            refresh.Token,
            access.ExpiresAt,
            refresh.ExpiresAt
        );
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
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new UnauthorizedAccessException("Account is not active.");
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);

        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = user.Id,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceFingerprint = request.DeviceFingerprint,
            DeviceName = request.DeviceName,
            WasSuccessful = validPassword,
            FailureReason = validPassword ? null : "Invalid password"
        });

        if (!validPassword)
        {
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var refresh = _jwtTokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh.Token,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);

        return new AuthResponseDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            access.Token,
            refresh.Token,
            access.ExpiresAt,
            refresh.ExpiresAt
        );
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(
    RefreshTokenRequestDto request,
    string? ipAddress,
    CancellationToken ct = default)
    {
        var existingRefreshToken = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, ct);

        if (existingRefreshToken is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!existingRefreshToken.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        var user = existingRefreshToken.User;

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        existingRefreshToken.IsRevoked = true;
        existingRefreshToken.RevokedAt = DateTime.UtcNow;
        existingRefreshToken.RevokedByIp = ipAddress;

        var newRefresh = _jwtTokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefresh.Token,
            ExpiresAt = newRefresh.ExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync(ct);

        var access = await _jwtTokenService.GenerateAccessTokenAsync(user);

        return new AuthResponseDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            access.Token,
            newRefresh.Token,
            access.ExpiresAt,
            newRefresh.ExpiresAt
        );
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        return new CurrentUserDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.CountryCode,
            user.UserType,
            user.Status
        );
    }
}