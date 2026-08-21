using System.Security.Cryptography;

namespace KorridorX.Services.EmbeddedFinance;

public static class EmbeddedWebhookSecret
{
    public const string Prefix = "whsec_";

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Prefix + Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
