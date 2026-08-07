using System.Security.Cryptography;
using System.Text;

namespace KorridorX.Services.Security;

public static class RefreshTokenSecurity
{
    public static string Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Refresh token is required.", nameof(token));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
    }
}
