using System.Text.Json;

namespace KorridorX.Services.WalletWithdrawals;

// Pure safety policy. No money movement, provider calls or database writes.
public static class WalletWithdrawalRecoveryPolicy
{
    public const string BlaaizNoteLimitMessage =
        "The note must not be greater than 50 characters.";

    public static string? ReadKnownNoteValidationError(string? responseBodyJson)
    {
        if (string.IsNullOrWhiteSpace(responseBodyJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(responseBodyJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("errors", out var errors) ||
                errors.ValueKind != JsonValueKind.Object ||
                !errors.TryGetProperty("note", out var notes) ||
                notes.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var note in notes.EnumerateArray())
            {
                if (note.ValueKind == JsonValueKind.String && string.Equals(
                        note.GetString(), BlaaizNoteLimitMessage, StringComparison.Ordinal))
                    return BlaaizNoteLimitMessage;
            }
        }
        catch (JsonException)
        {
            // Do not surface unrecognized or malformed provider payloads.
        }
        return null;
    }

    public static TimeSpan CooldownAfterNotFound(int consecutive404)
    {
        if (consecutive404 <= 0) return TimeSpan.Zero;
        var seconds = Math.Min(3600, 30 * (1 << Math.Min(consecutive404, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    public static bool ShouldDeferAutomaticLookup(
        DateTime nowUtc, DateTime lastGetUtc, int consecutive404) =>
        consecutive404 > 0 && nowUtc < lastGetUtc + CooldownAfterNotFound(consecutive404);
}
