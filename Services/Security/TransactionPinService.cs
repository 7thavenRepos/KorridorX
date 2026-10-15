using System.Security.Cryptography;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Audit;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Security;

public sealed class TransactionPinService : ITransactionPinService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly TransactionPinSecurityOptions _options;
    private readonly ConsumerExternalAuthOptions _externalOptions;
    private readonly ExternalAuthChallengeService _challenges;
    private readonly IExternalProviderTokenVerifier _verifier;

    public TransactionPinService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        IOptions<SecurityOptions> securityOptions,
        IOptions<ConsumerExternalAuthOptions> externalOptions,
        ExternalAuthChallengeService challenges,
        IExternalProviderTokenVerifier verifier)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _options = securityOptions.Value.TransactionPin;
        _externalOptions = externalOptions.Value;
        _challenges = challenges;
        _verifier = verifier;
    }

    public async Task<TransactionPinStatusDto> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        return await ToStatusAsync(user);
    }

    public async Task<TransactionPinStatusDto> SetAsync(
        Guid userId,
        string currentPassword,
        string pin,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        ValidatePin(pin);

        if (string.IsNullOrWhiteSpace(currentPassword) ||
            !await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        user.TransactionPinHash = _userManager.PasswordHasher.HashPassword(user, pin);
        user.TransactionPinFailedAttempts = 0;
        user.TransactionPinLockedUntil = null;
        user.TransactionPinUpdatedAt = DateTime.UtcNow;
        user.LastUpdatedAt = DateTime.UtcNow;

        _audit.Stage(new AuditRecordRequest(
            Action: "TRANSACTION_PIN_CONFIGURED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new { IsConfigured = true },
            UserId: user.Id));

        await _db.SaveChangesAsync(ct);
        return await ToStatusAsync(user);
    }

    public async Task<ConsumerExternalChallengeDto> CreateGoogleChallengeAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        await RequireGoogleLinkAsync(user);
        return _challenges.CreatePinChallenge(user.Id);
    }

    public async Task<TransactionPinStatusDto> SetWithGoogleAsync(
        Guid userId,
        SetGoogleTransactionPinRequestDto request,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        ValidatePin(request.Pin);
        await RequireGoogleLinkAsync(user);

        // The protected challenge is user-bound and purpose-bound. A login or
        // link challenge cannot authorize a PIN change.
        var challenge = _challenges.ReadPinChallenge(request.ChallengeToken, user.Id);
        var proof = await _verifier.VerifyAsync("google", request.IdToken, challenge.Nonce, ct);
        if (proof.Provider != "google") throw ExternalIdentityPolicy.InvalidToken();
        // A matching email is never sufficient: require the exact provider subject
        // to be linked to the account holding this authenticated access token.
        var owner = await _userManager.FindByLoginAsync("KorridorX.Google", proof.Subject);
        if (owner?.Id != user.Id) throw ExternalIdentityPolicy.InvalidToken();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await ClaimGoogleProofAsync(user.Id, challenge, request.IdToken, ct);
        user.TransactionPinHash = _userManager.PasswordHasher.HashPassword(user, request.Pin);
        user.TransactionPinFailedAttempts = 0;
        user.TransactionPinLockedUntil = null;
        user.TransactionPinUpdatedAt = DateTime.UtcNow;
        user.LastUpdatedAt = DateTime.UtcNow;

        _audit.Stage(new AuditRecordRequest(
            Action: "TRANSACTION_PIN_CONFIGURED",
            Category: "Authentication",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new { IsConfigured = true, VerificationMethod = "Google" },
            UserId: user.Id));
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ToStatusAsync(user);
    }

    private async Task RequireGoogleLinkAsync(ApplicationUser user)
    {
        if (!_externalOptions.Google.Enabled || _externalOptions.Google.ClientIds.Length == 0 ||
            !(await _userManager.GetLoginsAsync(user)).Any(x => x.LoginProvider == "KorridorX.Google"))
            throw new InvalidOperationException("Link an enabled Google account before using Google to configure a transaction PIN.");
    }

    private async Task ClaimGoogleProofAsync(Guid userId, ExternalAuthChallenge challenge, string token, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Google PIN verification requires a database transaction.");
        var now = DateTimeOffset.UtcNow;
        if (challenge.ExpiresAt <= now) throw ExternalIdentityPolicy.InvalidToken();
        // Share the existing login/link replay namespace so one provider proof
        // cannot be consumed by multiple sensitive operations.
        const string replayProvider = "KorridorX.ExternalAuthReplay";
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "AspNetUserTokens" WHERE "UserId" = {userId}
              AND "LoginProvider" = {replayProvider} AND CAST("Value" AS timestamptz) <= {now.UtcDateTime}
            """, ct);
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        foreach (var name in new[] { "Challenge:" + challenge.Id.ToString("N"), "Token:" + tokenHash })
        {
            var expiry = now.AddMinutes(10).ToString("O");
            var inserted = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value")
                VALUES ({userId}, {replayProvider}, {name}, {expiry})
                ON CONFLICT ("UserId", "LoginProvider", "Name") DO NOTHING
                """, ct);
            if (inserted != 1) throw ExternalIdentityPolicy.InvalidToken();
        }
    }

    public async Task VerifyForTransactionAsync(
        Guid userId,
        string? pin,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(user.TransactionPinHash))
        {
            throw new InvalidOperationException(
                "Set a transaction PIN in Security before creating a transfer.");
        }

        var now = DateTime.UtcNow;
        if (user.TransactionPinLockedUntil is { } lockedUntil && lockedUntil > now)
        {
            throw new InvalidOperationException(
                $"Transaction PIN is temporarily locked until {lockedUntil:O}.");
        }

        var result = string.IsNullOrEmpty(pin)
            ? PasswordVerificationResult.Failed
            : _userManager.PasswordHasher.VerifyHashedPassword(
                user,
                user.TransactionPinHash,
                pin);

        if (result == PasswordVerificationResult.Failed)
        {
            user.TransactionPinFailedAttempts++;
            if (user.TransactionPinFailedAttempts >= _options.MaximumVerificationAttempts)
            {
                user.TransactionPinLockedUntil = now.AddMinutes(_options.LockoutMinutes);
                user.TransactionPinFailedAttempts = 0;
            }

            user.LastUpdatedAt = now;
            _audit.Stage(new AuditRecordRequest(
                Action: "TRANSACTION_PIN_VERIFICATION_FAILED",
                Category: "Authentication",
                EntityName: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                NewValues: new
                {
                    Locked = user.TransactionPinLockedUntil > now,
                    user.TransactionPinLockedUntil
                },
                UserId: user.Id));
            await _db.SaveChangesAsync(ct);

            throw new InvalidOperationException(
                user.TransactionPinLockedUntil > now
                    ? "Transaction PIN is temporarily locked. Try again later."
                    : "Transaction PIN is incorrect.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.TransactionPinHash = _userManager.PasswordHasher.HashPassword(user, pin!);
        }

        if (user.TransactionPinFailedAttempts != 0 ||
            user.TransactionPinLockedUntil is not null ||
            result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.TransactionPinFailedAttempts = 0;
            user.TransactionPinLockedUntil = null;
            user.LastUpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<ApplicationUser> GetConsumerAsync(
        Guid userId,
        CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("Authenticated user was not found.");

        if (user.UserType != UserType.Consumer || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(
                "Transaction PIN is available only to active consumer accounts.");
        }

        return user;
    }

    private void ValidatePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin) ||
            pin.Length < _options.MinimumLength ||
            pin.Length > _options.MaximumLength ||
            pin.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new InvalidOperationException(
                $"Transaction PIN must contain {_options.MinimumLength} to {_options.MaximumLength} digits.");
        }
    }

    private async Task<TransactionPinStatusDto> ToStatusAsync(ApplicationUser user)
    {
        var googleEnabled = _externalOptions.Google.Enabled && _externalOptions.Google.ClientIds.Length > 0;
        var googleLinked = googleEnabled &&
            (await _userManager.GetLoginsAsync(user)).Any(x => x.LoginProvider == "KorridorX.Google");
        return new(
            !string.IsNullOrWhiteSpace(user.TransactionPinHash),
            _options.MinimumLength,
            _options.MaximumLength,
            user.TransactionPinLockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow ? lockedUntil : null,
            user.TransactionPinUpdatedAt,
            googleLinked);
    }
}
