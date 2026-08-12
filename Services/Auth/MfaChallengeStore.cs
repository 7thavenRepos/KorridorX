using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Auth;

public sealed record StoredMfaChallenge(
    Guid ChallengeId,
    Guid UserId,
    string TokenHash,
    string Purpose,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    int AttemptCount,
    int MaximumAttempts,
    string? IpAddress,
    string? UserAgent,
    string? DeviceFingerprint,
    string? DeviceName);

public sealed record MfaChallengeRedemption(
    StoredMfaChallenge Challenge,
    string TokenName,
    string ExpectedValue);

public interface IMfaChallengeStore
{
    Task<MfaChallengeDto> CreateAsync(
        Guid userId,
        string purpose,
        string? ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        string? deviceName,
        CancellationToken ct = default);

    Task<StoredMfaChallenge> ReadAsync(
        MfaEnrollmentSetupRequestDto request,
        string expectedPurpose,
        CancellationToken ct = default);

    Task<MfaChallengeRedemption> AdvanceAsync(
        MfaVerificationRequestDto request,
        string expectedPurpose,
        CancellationToken ct = default);

    Task<bool> ConsumeAsync(
        MfaChallengeRedemption redemption,
        CancellationToken ct = default);

    Task<bool> TryClaimCodeAsync(
        Guid userId,
        string authenticatorKey,
        string normalizedCode,
        CancellationToken ct = default);

    Task<DateTime?> GetEnrollmentEpochAsync(
        Guid userId,
        CancellationToken ct = default);

    Task SetEnrollmentEpochAsync(
        Guid userId,
        DateTime enrolledAt,
        CancellationToken ct = default);

    Task ClearAsync(Guid userId, CancellationToken ct = default);
}

public sealed class MfaChallengeStore : IMfaChallengeStore
{
    private const string LoginProvider = "KorridorX.MfaSecurity";
    private const string ChallengePrefix = "Challenge:";
    private const string UsedCodePrefix = "UsedCode:";
    private const string EnrollmentEpochName = "EnrollmentEpoch";
    private const long ReplayBucketSeconds = 600;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly MfaSecurityOptions _options;

    public MfaChallengeStore(
        AppDbContext db,
        IOptions<SecurityOptions> securityOptions)
    {
        _db = db;
        _options = securityOptions.Value.Mfa;
    }

    public async Task<MfaChallengeDto> CreateAsync(
        Guid userId,
        string purpose,
        string? ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        string? deviceName,
        CancellationToken ct = default)
    {
        await _db.UserTokens
            .Where(x =>
                x.UserId == userId &&
                x.LoginProvider == LoginProvider &&
                x.Name.StartsWith(ChallengePrefix))
            .ExecuteDeleteAsync(ct);

        var now = DateTime.UtcNow;
        var challengeId = Guid.NewGuid();
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = new StoredMfaChallenge(
            challengeId,
            userId,
            HashToken(rawToken),
            purpose,
            now,
            now.AddMinutes(_options.ChallengeLifespanMinutes),
            0,
            _options.MaximumVerificationAttempts,
            Clean(ipAddress, 100),
            Clean(userAgent, 1000),
            Clean(deviceFingerprint, 250),
            Clean(deviceName, 250));

        _db.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = userId,
            LoginProvider = LoginProvider,
            Name = ChallengeName(challengeId),
            Value = JsonSerializer.Serialize(challenge, JsonOptions)
        });

        return new MfaChallengeDto(
            challengeId,
            rawToken,
            purpose,
            challenge.ExpiresAt,
            challenge.MaximumAttempts);
    }

    public async Task<StoredMfaChallenge> ReadAsync(
        MfaEnrollmentSetupRequestDto request,
        string expectedPurpose,
        CancellationToken ct = default)
    {
        var stored = await FindValidAsync(
            request.ChallengeId,
            request.ChallengeToken,
            expectedPurpose,
            ct);

        return stored.Challenge;
    }

    public async Task<MfaChallengeRedemption> AdvanceAsync(
        MfaVerificationRequestDto request,
        string expectedPurpose,
        CancellationToken ct = default)
    {
        var stored = await FindValidAsync(
            request.ChallengeId,
            request.ChallengeToken,
            expectedPurpose,
            ct);

        var advancedChallenge = stored.Challenge with
        {
            AttemptCount = stored.Challenge.AttemptCount + 1
        };
        var advancedValue = JsonSerializer.Serialize(advancedChallenge, JsonOptions);

        var updated = await _db.UserTokens
            .Where(x =>
                x.UserId == stored.Challenge.UserId &&
                x.LoginProvider == LoginProvider &&
                x.Name == stored.TokenName &&
                x.Value == stored.StoredValue)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Value, advancedValue),
                ct);

        if (updated != 1)
            throw InvalidChallenge();

        return new MfaChallengeRedemption(
            advancedChallenge,
            stored.TokenName,
            advancedValue);
    }

    public async Task<bool> ConsumeAsync(
        MfaChallengeRedemption redemption,
        CancellationToken ct = default)
    {
        var deleted = await _db.UserTokens
            .Where(x =>
                x.UserId == redemption.Challenge.UserId &&
                x.LoginProvider == LoginProvider &&
                x.Name == redemption.TokenName &&
                x.Value == redemption.ExpectedValue)
            .ExecuteDeleteAsync(ct);

        return deleted == 1;
    }

    public async Task<bool> TryClaimCodeAsync(
        Guid userId,
        string authenticatorKey,
        string normalizedCode,
        CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "MFA code replay claims require an active database transaction.");
        }

        var currentBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / ReplayBucketSeconds;
        var codeHash = HashAuthenticatorCode(userId, authenticatorKey, normalizedCode);
        var currentName = UsedCodeName(currentBucket, codeHash);
        var previousName = UsedCodeName(currentBucket - 1, codeHash);
        var cutoffBucket = currentBucket - 1;

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "AspNetUserTokens"
            WHERE "UserId" = {userId}
              AND "LoginProvider" = {LoginProvider}
              AND "Name" LIKE {UsedCodePrefix + "%"}
              AND CAST(split_part("Name", ':', 2) AS bigint) < {cutoffBucket}
            """, ct);

        var expiresAt = DateTime.UtcNow.AddSeconds(ReplayBucketSeconds * 2).ToString("O");
        var insertedCurrent = await InsertReplayClaimAsync(
            userId,
            currentName,
            expiresAt,
            ct);
        var insertedPrevious = await InsertReplayClaimAsync(
            userId,
            previousName,
            expiresAt,
            ct);

        return insertedCurrent == 1 && insertedPrevious == 1;
    }

    public async Task<DateTime?> GetEnrollmentEpochAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var value = await _db.UserTokens
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.LoginProvider == LoginProvider &&
                x.Name == EnrollmentEpochName)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        return DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var enrolledAt)
            ? enrolledAt.ToUniversalTime()
            : null;
    }

    public async Task SetEnrollmentEpochAsync(
        Guid userId,
        DateTime enrolledAt,
        CancellationToken ct = default)
    {
        var value = enrolledAt.ToUniversalTime().ToString("O");
        var token = await _db.UserTokens.FirstOrDefaultAsync(
            x =>
                x.UserId == userId &&
                x.LoginProvider == LoginProvider &&
                x.Name == EnrollmentEpochName,
            ct);

        if (token is null)
        {
            _db.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = userId,
                LoginProvider = LoginProvider,
                Name = EnrollmentEpochName,
                Value = value
            });
        }
        else
        {
            token.Value = value;
        }
    }

    public async Task ClearAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.UserTokens
            .Where(x => x.UserId == userId && x.LoginProvider == LoginProvider)
            .ExecuteDeleteAsync(ct);
    }

    private async Task<StoredChallengeRow> FindValidAsync(
        Guid challengeId,
        string rawToken,
        string expectedPurpose,
        CancellationToken ct)
    {
        if (challengeId == Guid.Empty || string.IsNullOrWhiteSpace(rawToken))
            throw InvalidChallenge();

        var tokenName = ChallengeName(challengeId);
        var row = await _db.UserTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LoginProvider == LoginProvider && x.Name == tokenName,
                ct);

        if (row?.Value is null)
            throw InvalidChallenge();

        StoredMfaChallenge? challenge;
        try
        {
            challenge = JsonSerializer.Deserialize<StoredMfaChallenge>(row.Value, JsonOptions);
        }
        catch (JsonException)
        {
            challenge = null;
        }

        if (challenge is null ||
            challenge.ChallengeId != challengeId ||
            !string.Equals(challenge.Purpose, expectedPurpose, StringComparison.Ordinal) ||
            !TokenMatches(challenge.TokenHash, rawToken))
        {
            throw InvalidChallenge();
        }

        if (challenge.ExpiresAt <= DateTime.UtcNow ||
            challenge.AttemptCount >= challenge.MaximumAttempts)
        {
            await _db.UserTokens
                .Where(x =>
                    x.UserId == challenge.UserId &&
                    x.LoginProvider == LoginProvider &&
                    x.Name == tokenName &&
                    x.Value == row.Value)
                .ExecuteDeleteAsync(ct);
            throw InvalidChallenge();
        }

        return new StoredChallengeRow(challenge, tokenName, row.Value);
    }

    private async Task<int> InsertReplayClaimAsync(
        Guid userId,
        string name,
        string value,
        CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value")
            VALUES ({userId}, {LoginProvider}, {name}, {value})
            ON CONFLICT ("UserId", "LoginProvider", "Name") DO NOTHING
            """, ct);

    private string HashAuthenticatorCode(
        Guid userId,
        string authenticatorKey,
        string normalizedCode)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.CodeReplayPepper));
        var payload = Encoding.UTF8.GetBytes(
            $"{userId:N}:{authenticatorKey}:{normalizedCode}");
        return Convert.ToHexString(hmac.ComputeHash(payload));
    }

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static bool TokenMatches(string storedHash, string rawToken)
    {
        if (storedHash.Length != 64)
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(storedHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ChallengeName(Guid challengeId) =>
        $"{ChallengePrefix}{challengeId:N}";

    private static string UsedCodeName(long bucket, string codeHash) =>
        $"{UsedCodePrefix}{bucket:D12}:{codeHash}";

    private static UnauthorizedAccessException InvalidChallenge() =>
        new("The MFA challenge is invalid, expired, or has already been used.");

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private sealed record StoredChallengeRow(
        StoredMfaChallenge Challenge,
        string TokenName,
        string StoredValue);
}
