using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Audit;
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

    public TransactionPinService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        IOptions<SecurityOptions> securityOptions)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _options = securityOptions.Value.TransactionPin;
    }

    public async Task<TransactionPinStatusDto> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await GetConsumerAsync(userId, ct);
        return ToStatus(user);
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
        return ToStatus(user);
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

    private TransactionPinStatusDto ToStatus(ApplicationUser user) => new(
        !string.IsNullOrWhiteSpace(user.TransactionPinHash),
        _options.MinimumLength,
        _options.MaximumLength,
        user.TransactionPinLockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow
            ? lockedUntil
            : null,
        user.TransactionPinUpdatedAt);
}
