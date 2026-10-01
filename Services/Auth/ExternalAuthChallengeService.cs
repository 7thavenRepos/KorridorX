using System.Security.Cryptography;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Dtos.Auth;
using KorridorX.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Auth;

public sealed record ExternalAuthChallenge(Guid Id, string Provider, string Purpose, Guid? UserId,
    string Nonce, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

public sealed class ExternalAuthChallengeService
{
    private readonly IDataProtector _protector;
    private readonly ConsumerExternalAuthOptions _options;
    public ExternalAuthChallengeService(IDataProtectionProvider protection, IOptions<ConsumerExternalAuthOptions> options)
    { _protector = protection.CreateProtector("KorridorX.ConsumerExternalAuth.v1"); _options = options.Value; }

    public ExternalIdentityProviderOptions Enabled(string provider)
    {
        provider = ExternalIdentityPolicy.Provider(provider);
        var value = provider == "google" ? _options.Google : _options.Apple;
        if (!value.Enabled || value.ClientIds.Length == 0)
            throw new ForbiddenException("This sign-in provider is not configured for the Consumer app.");
        return value;
    }

    public ConsumerExternalChallengeDto Create(string provider, Guid? userId)
    {
        provider = ExternalIdentityPolicy.Provider(provider);
        Enabled(provider);
        var now = DateTimeOffset.UtcNow;
        var random = RandomNumberGenerator.GetBytes(32);
        // Native Apple receives the SHA-256 nonce directly; the client must not hash it again.
        var nonce = provider == "apple" ? Convert.ToHexString(SHA256.HashData(random)).ToLowerInvariant() : WebEncoders.Base64UrlEncode(random);
        var challenge = new ExternalAuthChallenge(Guid.NewGuid(), provider,
            userId.HasValue ? "link" : "signin", userId, nonce, now, now.AddMinutes(5));
        return new(provider, _protector.Protect(JsonSerializer.Serialize(challenge)), nonce, challenge.ExpiresAt);
    }

    public ExternalAuthChallenge Read(string token, string provider, Guid? userId)
    {
        provider = ExternalIdentityPolicy.Provider(provider);
        Enabled(provider);
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) throw ExternalIdentityPolicy.InvalidToken();
            var value = JsonSerializer.Deserialize<ExternalAuthChallenge>(_protector.Unprotect(token));
            var now = DateTimeOffset.UtcNow;
            if (value is null || value.Id == Guid.Empty || value.Provider != provider || value.UserId != userId ||
                value.Purpose != (userId.HasValue ? "link" : "signin") || value.ExpiresAt <= now ||
                value.IssuedAt > now.AddSeconds(30) || value.ExpiresAt > value.IssuedAt.AddMinutes(5) ||
                value.IssuedAt < now.AddMinutes(-5) || value.Nonce.Length is < 32 or > 128) throw ExternalIdentityPolicy.InvalidToken();
            return value;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw ExternalIdentityPolicy.InvalidToken(); }
    }
}
