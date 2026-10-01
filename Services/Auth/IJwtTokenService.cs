using KorridorX.Models.Identity;

namespace KorridorX.Services.Auth;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAt)> GenerateAccessTokenAsync(
        ApplicationUser user,
        bool mfaAuthenticated = false,
        string authenticationMethod = MfaSecurityPolicy.PasswordAuthenticationMethod);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}
