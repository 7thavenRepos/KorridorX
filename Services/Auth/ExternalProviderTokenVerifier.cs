using System.IdentityModel.Tokens.Jwt;
using KorridorX.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KorridorX.Services.Auth;

public sealed record VerifiedExternalIdentity(string Provider, string Subject);
public interface IExternalProviderTokenVerifier
{
    Task<VerifiedExternalIdentity> VerifyAsync(string provider, string idToken, string nonce, CancellationToken ct);
}

public sealed class ExternalProviderTokenVerifier(ExternalAuthChallengeService challenges, IExternalSigningKeySource keys)
    : IExternalProviderTokenVerifier
{
    public async Task<VerifiedExternalIdentity> VerifyAsync(string provider, string idToken, string nonce, CancellationToken ct)
    {
        provider = ExternalIdentityPolicy.Provider(provider);
        var options = challenges.Enabled(provider);
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 16384) throw ExternalIdentityPolicy.InvalidToken();
        var parts = idToken.Split('.');
        if (parts.Length != 3) throw ExternalIdentityPolicy.InvalidToken();
        using var header = ExternalIdentityPolicy.ReadObject(parts[0]);
        using var payload = ExternalIdentityPolicy.ReadObject(parts[1]);
        if (ExternalIdentityPolicy.Text(header.RootElement, "alg") != SecurityAlgorithms.RsaSha256) throw ExternalIdentityPolicy.InvalidToken();
        var kid = ExternalIdentityPolicy.Text(header.RootElement, "kid");
        if (kid.Length is 0 or > 128) throw ExternalIdentityPolicy.InvalidToken();
        // Reject invalid claims before a key fetch; still verify the full JWT signature below.
        var subject = ExternalIdentityPolicy.Subject(payload.RootElement, provider, nonce, options.ClientIds, options.PresenterClientIds, DateTimeOffset.UtcNow);
        var signingKeys = await keys.GetAsync(provider, kid, ct);
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16384 };
            handler.ValidateToken(idToken, new TokenValidationParameters
            {
                RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKeys = signingKeys,
                TryAllIssuerSigningKeys = false, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateIssuer = true, ValidIssuers = provider == "google" ? ["accounts.google.com", "https://accounts.google.com"] : ["https://appleid.apple.com"],
                ValidateAudience = true, ValidAudiences = options.ClientIds,
                RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        { throw ExternalIdentityPolicy.InvalidToken(); }
        return new(provider, subject);
    }
}
