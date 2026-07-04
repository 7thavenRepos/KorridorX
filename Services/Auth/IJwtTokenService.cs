using KorridorX.Models.Identity;

namespace KorridorX.Services.Auth;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAt)> GenerateAccessTokenAsync(ApplicationUser user);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}