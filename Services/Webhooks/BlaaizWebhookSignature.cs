using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KorridorX.Services.Webhooks;

public static class BlaaizWebhookSignature
{
    public static string Create(string timestamp, string payload, string secret)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            throw new ArgumentException("Webhook timestamp is required.", nameof(timestamp));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Webhook payload is required.", nameof(payload));
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Webhook signing secret is required.", nameof(secret));

        var canonical = Canonicalize(payload);
        return Compute($"{timestamp}.{canonical}", secret);
    }

    public static bool IsValid(
        string payload,
        string? receivedSignature,
        string? timestamp,
        string secret,
        TimeSpan tolerance,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(receivedSignature) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        DateTimeOffset webhookTime;
        try
        {
            webhookTime = ParseTimestamp(timestamp);
        }
        catch
        {
            return false;
        }

        if ((now - webhookTime).Duration() > tolerance)
            return false;

        var actual = receivedSignature.Trim();
        if (actual.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            actual = actual[7..];

        var canonical = Canonicalize(payload);
        if (FixedTimeEquals(Compute($"{timestamp}.{canonical}", secret), actual))
            return true;

        return !string.Equals(canonical, payload, StringComparison.Ordinal) &&
               FixedTimeEquals(Compute($"{timestamp}.{payload}", secret), actual);
    }

    private static string Canonicalize(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return JsonSerializer.Serialize(json.RootElement);
    }

    private static string Compute(string signedContent, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)))
            .ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected.ToLowerInvariant());
        var actualBytes = Encoding.UTF8.GetBytes(actual.ToLowerInvariant());
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static DateTimeOffset ParseTimestamp(string timestamp)
    {
        if (long.TryParse(timestamp, out var numeric))
        {
            return numeric > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(numeric)
                : DateTimeOffset.FromUnixTimeSeconds(numeric);
        }

        return DateTimeOffset.Parse(
            timestamp,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal);
    }
}
