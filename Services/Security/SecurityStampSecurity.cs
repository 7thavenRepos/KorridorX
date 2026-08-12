using System.Security.Cryptography;
using System.Text;

namespace KorridorX.Services.Security;

public static class SecurityStampSecurity
{
    public const string ClaimType = "sst";

    public static string Hash(string? securityStamp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? ""));
        return Convert.ToHexString(bytes);
    }

    public static bool Matches(string? claimedHash, string? currentSecurityStamp)
    {
        if (string.IsNullOrWhiteSpace(claimedHash))
            return false;

        byte[] claimedBytes;
        try
        {
            claimedBytes = Convert.FromHexString(claimedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var currentBytes = SHA256.HashData(Encoding.UTF8.GetBytes(currentSecurityStamp ?? ""));
        return claimedBytes.Length == currentBytes.Length &&
               CryptographicOperations.FixedTimeEquals(claimedBytes, currentBytes);
    }
}
