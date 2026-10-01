using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Auth;

// Claim checks alone never authorize a credential: the JWT verifier independently
// verifies its signature. This policy is shared with deterministic regressions.
public static class ExternalIdentityPolicy
{
    public static string Provider(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "google" => "google",
        "apple" => "apple",
        _ => throw new InvalidOperationException("Select a supported sign-in provider.")
    };

    public static string LoginProvider(string provider) => Provider(provider) == "google"
        ? "KorridorX.Google" : "KorridorX.Apple";

    public static void RequireConsumer(UserType type, IEnumerable<string> roles)
    {
        var values = roles.ToArray();
        if (type != UserType.Consumer || values.Length == 0 ||
            values.Any(x => !string.Equals(x, "Consumer", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedApiException("This sign-in channel is available to Consumer accounts only.", "CONSUMER_CHANNEL_REQUIRED");
    }

    public static JsonDocument ReadObject(string encoded)
    {
        try
        {
            if (encoded.Length is 0 or > 16384 || encoded.Any(x => !(char.IsAsciiLetterOrDigit(x) || x is '-' or '_')))
                throw InvalidToken();
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            var doc = JsonDocument.Parse(Convert.FromBase64String(base64));
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                doc.RootElement.EnumerateObject().Any(x => !names.Add(x.Name)))
            { doc.Dispose(); throw InvalidToken(); }
            return doc;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        { throw InvalidToken(); }
    }

    public static string Subject(JsonElement payload, string provider, string expectedNonce,
        IReadOnlyCollection<string> audiences, IReadOnlyCollection<string> presenters, DateTimeOffset now)
    {
        var issuer = Text(payload, "iss");
        var validIssuer = Provider(provider) == "google"
            ? issuer is "accounts.google.com" or "https://accounts.google.com"
            : issuer == "https://appleid.apple.com";
        var audience = Text(payload, "aud");
        var sub = Text(payload, "sub");
        if (!validIssuer || !audiences.Contains(audience, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(sub) || sub != sub.Trim() || sub.Length > 128 || sub.Any(char.IsControl) ||
            !EqualNonce(Text(payload, "nonce"), expectedNonce)) throw InvalidToken();
        if (payload.TryGetProperty("azp", out var azp) &&
            (azp.ValueKind != JsonValueKind.String ||
             !audiences.Concat(presenters).Contains(azp.GetString(), StringComparer.Ordinal))) throw InvalidToken();
        var iat = Number(payload, "iat");
        var exp = Number(payload, "exp");
        var seconds = now.ToUnixTimeSeconds();
        if (iat > seconds + 30 || iat < seconds - 300 || exp <= seconds || exp <= iat) throw InvalidToken();
        if (payload.TryGetProperty("nbf", out _) && Number(payload, "nbf") > seconds + 30) throw InvalidToken();
        // Email/name/relay claims never resolve, create or merge an account.
        return sub;
    }

    public static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : throw InvalidToken();

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number : throw InvalidToken();

    private static bool EqualNonce(string actual, string expected) =>
        !string.IsNullOrEmpty(expected) && actual.Length == expected.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));

    public static UnauthorizedApiException InvalidToken() =>
        new("The provider credential is invalid, expired, or has already been used. Start sign-in again.", "EXTERNAL_CREDENTIAL_INVALID");
}
