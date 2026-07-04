using KorridorX.Dtos.Auth;

namespace KorridorX.Services.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, string? ipAddress, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}