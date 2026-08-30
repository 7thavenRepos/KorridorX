using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace KorridorX.Services.BusinessTransfers;

public static class BusinessInvitationToken
{
    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("The business invitation is invalid or has expired.");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }
}
