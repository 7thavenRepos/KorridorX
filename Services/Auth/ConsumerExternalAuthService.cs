using System.Security.Cryptography;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Auth;
using KorridorX.Exceptions;
using KorridorX.Models.Identity;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Auth;

public interface IConsumerExternalAuthService
{
    IReadOnlyList<ConsumerExternalProviderDto> Providers();
    ConsumerExternalChallengeDto SignInChallenge(string provider);
    Task<ConsumerExternalChallengeDto> LinkChallengeAsync(Guid userId, string provider, CancellationToken ct);
    Task<IReadOnlyList<ConsumerExternalLinkedProviderDto>> LinksAsync(Guid userId, CancellationToken ct);
    Task<LoginResultDto> LoginAsync(ConsumerExternalLoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken ct);
    Task<ConsumerExternalLinkResultDto> LinkAsync(Guid userId, ConsumerExternalLinkRequestDto request, string? ipAddress, CancellationToken ct);
}

public sealed class ConsumerExternalAuthService(UserManager<ApplicationUser> users, AppDbContext db,
    AuthService auth, ExternalAuthChallengeService challenges, IExternalProviderTokenVerifier verifier,
    IOptions<ConsumerExternalAuthOptions> options, IAuditService audit) : IConsumerExternalAuthService
{
    private const string ReplayProvider = "KorridorX.ExternalAuthReplay";

    public IReadOnlyList<ConsumerExternalProviderDto> Providers() =>
        [new("google", options.Value.Google.Enabled), new("apple", options.Value.Apple.Enabled)];
    public ConsumerExternalChallengeDto SignInChallenge(string provider) => challenges.Create(provider, null);

    public async Task<ConsumerExternalChallengeDto> LinkChallengeAsync(Guid userId, string provider, CancellationToken ct)
    { await auth.ExternalConsumerAsync(userId, ct); return challenges.Create(provider, userId); }

    public async Task<IReadOnlyList<ConsumerExternalLinkedProviderDto>> LinksAsync(Guid userId, CancellationToken ct)
    {
        var user = await auth.ExternalConsumerAsync(userId, ct);
        return (await users.GetLoginsAsync(user)).Where(x => x.LoginProvider is "KorridorX.Google" or "KorridorX.Apple")
            .Select(x => new ConsumerExternalLinkedProviderDto(x.LoginProvider == "KorridorX.Google" ? "google" : "apple")).ToArray();
    }

    public async Task<LoginResultDto> LoginAsync(ConsumerExternalLoginRequestDto request, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var challenge = challenges.Read(request.ChallengeToken, request.Provider, null);
        var identity = await verifier.VerifyAsync(challenge.Provider, request.IdToken, challenge.Nonce, ct);
        if (identity.Provider != challenge.Provider) throw ExternalIdentityPolicy.InvalidToken();
        var linked = await users.FindByLoginAsync(ExternalIdentityPolicy.LoginProvider(identity.Provider), identity.Subject);
        if (linked is null)
            throw new UnauthorizedApiException("Sign in to your Consumer account to link this provider, or create an account in the Consumer app first.", "EXTERNAL_ACCOUNT_LINK_REQUIRED");
        var user = await auth.ExternalConsumerAsync(linked.Id, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ClaimProofAsync(user.Id, challenge, request.IdToken, ct);
        var result = await auth.CompleteExternalLoginAsync(user, request, ipAddress, userAgent, ct);
        audit.Stage(new AuditRecordRequest(Action: "CONSUMER_EXTERNAL_SIGN_IN", Category: "Authentication",
            EntityName: nameof(ApplicationUser), EntityId: user.Id.ToString(), Metadata: new { identity.Provider, result.Status }, UserId: user.Id));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<ConsumerExternalLinkResultDto> LinkAsync(Guid userId, ConsumerExternalLinkRequestDto request, string? ipAddress, CancellationToken ct)
    {
        var user = await auth.ExternalConsumerAsync(userId, ct);
        // An access token and matching email do not replace fresh account proof.
        await auth.VerifyExternalLinkPasswordAsync(user, request.CurrentPassword, ct);
        var challenge = challenges.Read(request.ChallengeToken, request.Provider, userId);
        var identity = await verifier.VerifyAsync(challenge.Provider, request.IdToken, challenge.Nonce, ct);
        if (identity.Provider != challenge.Provider) throw ExternalIdentityPolicy.InvalidToken();
        var loginProvider = ExternalIdentityPolicy.LoginProvider(identity.Provider);
        var owner = await users.FindByLoginAsync(loginProvider, identity.Subject);
        if (owner is not null) throw new InvalidOperationException("This provider identity is already linked. Existing links are preserved.");
        if ((await users.GetLoginsAsync(user)).Any(x => x.LoginProvider == loginProvider))
            throw new InvalidOperationException("This account already has a link for this provider. Existing links are preserved.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await auth.VerifyExternalLinkMfaAsync(user, request.MfaCode, request.MfaCodeType, ct))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            await auth.RecordExternalLinkFailureAsync(userId, ct);
            throw new UnauthorizedApiException("The current password and verification code could not be verified.", "EXTERNAL_LINK_REAUTHENTICATION_REQUIRED");
        }
        await ClaimProofAsync(userId, challenge, request.IdToken, ct);
        var result = await users.AddLoginAsync(user, new UserLoginInfo(loginProvider, identity.Subject,
            identity.Provider == "google" ? "Google" : "Apple"));
        if (!result.Succeeded) throw new InvalidOperationException("The provider identity could not be linked. Existing links are preserved.");
        await auth.CompleteExternalLinkSecurityAsync(user, identity.Provider, ipAddress, ct);
        audit.Stage(new AuditRecordRequest(Action: "CONSUMER_EXTERNAL_IDENTITY_LINKED", Category: "Authentication",
            EntityName: nameof(ApplicationUser), EntityId: user.Id.ToString(), Metadata: new { identity.Provider }, UserId: user.Id));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(identity.Provider, true, true);
    }

    private async Task ClaimProofAsync(Guid userId, ExternalAuthChallenge challenge, string token, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Provider proof claims require a database transaction.");
        var now = DateTimeOffset.UtcNow;
        if (challenge.ExpiresAt <= now) throw ExternalIdentityPolicy.InvalidToken();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "AspNetUserTokens" WHERE "UserId" = {userId}
              AND "LoginProvider" = {ReplayProvider} AND CAST("Value" AS timestamptz) <= {now.UtcDateTime}
            """, ct);
        var proof = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        // Provider nonce binds the proof to this signed challenge. Both records must
        // be inserted atomically; ON CONFLICT prevents reuse across API instances.
        foreach (var name in new[] { "Challenge:" + challenge.Id.ToString("N"), "Token:" + proof })
        {
            var expiry = now.AddMinutes(10).ToString("O");
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value")
                VALUES ({userId}, {ReplayProvider}, {name}, {expiry})
                ON CONFLICT ("UserId", "LoginProvider", "Name") DO NOTHING
                """, ct);
            if (inserted != 1) throw ExternalIdentityPolicy.InvalidToken();
        }
    }
}
