using System.Security.Cryptography;
using System.Text;

namespace KorridorX.Services.EmbeddedFinance;

public static class EmbeddedApiKey
{
    public const string Prefix = "kx_live_";
    public static GeneratedApiKey Generate()
    {
        var keyId = Guid.NewGuid().ToString("N");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new GeneratedApiKey(keyId, $"{Prefix}{keyId}.{secret}", ComputeSecretHash(secret), secret[^4..]);
    }
    public static bool TryParse(string value, out string keyId, out string secret)
    {
        keyId = ""; secret = "";
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var payload = value[Prefix.Length..];
        var separator = payload.IndexOf('.');
        if (separator <= 0 || separator == payload.Length - 1) return false;
        keyId = payload[..separator]; secret = payload[(separator + 1)..];
        return keyId.Length == 32 && secret.Length >= 32;
    }
    public static string ComputeSecretHash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    public static bool Verify(string secret, string expectedHash)
    {
        try
        {
            var actual = Convert.FromHexString(ComputeSecretHash(secret));
            var expected = Convert.FromHexString(expectedHash);
            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

public sealed record GeneratedApiKey(string KeyId, string ApiKey, string SecretHash, string SecretLastFour);
