using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Models.Audit;
using KorridorX.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

public static class InteracSecurity
{
    public const int MaximumSendingEmails = 5;
    public const int MaximumCodeAttempts = 5;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    public static string NormalizeEmail(string? value)
    {
        var raw = value?.Trim() ?? "";
        if (raw.Length is < 3 or > 254 || raw.Any(char.IsControl) || !MailAddress.TryCreate(raw, out var parsed) ||
            !string.Equals(parsed.Address, raw, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Enter one valid sending email address without a display name.");
        var at = raw.LastIndexOf('@');
        if (at < 1) throw new InvalidOperationException("Enter a valid email address.");
        var domain = new IdnMapping().GetAscii(raw[(at + 1)..]).ToLowerInvariant();
        var normalized = raw[..at].ToLowerInvariant() + "@" + domain;
        if (normalized.Length > 254) throw new InvalidOperationException("The email address is too long.");
        // Do not remove '+' suffixes or dots; those transformations are provider-specific.
        return normalized;
    }

    public static string? OptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return NormalizeEmail(value); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return null; }
    }

    public static string IssueCode(InteracSendingEmail sender, DateTime now)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var salt = RandomNumberGenerator.GetBytes(24);
        sender.CodeSalt = Convert.ToBase64String(salt);
        sender.CodeHash = Convert.ToBase64String(Derive(code, salt));
        sender.CodeExpiresAt = now.Add(CodeLifetime); sender.CanResendAt = now.Add(ResendCooldown);
        sender.FailedAttempts = 0; sender.Status = "Pending"; sender.Revision = Guid.NewGuid();
        return code;
    }

    public static bool CodeMatches(InteracSendingEmail sender, string? code, DateTime now) =>
        sender.Status == "Pending" && sender.CodeExpiresAt > now && sender.FailedAttempts < MaximumCodeAttempts &&
        sender.CodeSalt is not null && sender.CodeHash is not null && code is { Length: 6 } &&
        code.All(x => x is >= '0' and <= '9') && CryptographicOperations.FixedTimeEquals(
            Derive(code, Convert.FromBase64String(sender.CodeSalt)), Convert.FromBase64String(sender.CodeHash));

    public static void ClearCode(InteracSendingEmail sender)
    {
        sender.CodeHash = null; sender.CodeSalt = null; sender.CodeExpiresAt = null;
        sender.Revision = Guid.NewGuid();
    }

    private static byte[] Derive(string code, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(code), salt, 100_000, HashAlgorithmName.SHA256, 32);

    public static string Required(string? value, int max, string name)
    {
        var clean = value?.Trim() ?? "";
        if (clean.Length == 0 || clean.Length > max) throw new InvalidOperationException($"{name} must contain 1–{max} characters.");
        return clean;
    }

    public static Task LockAsync(AppDbContext db, string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

    public static void Audit(AppDbContext db, Guid? actor, string action, string entity, Guid id, string reason) =>
        db.AuditLogs.Add(new AuditLog { UserId = actor, Category = "InteracFunding", Action = action,
            EntityName = entity, EntityId = id.ToString(), MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { reason }) });
}
