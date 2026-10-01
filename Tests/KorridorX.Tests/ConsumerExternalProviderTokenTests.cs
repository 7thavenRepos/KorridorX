using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KorridorX.Tests;

public sealed class ConsumerExternalProviderTokenTests
{
    private const string Client = "owned.apps.googleusercontent.com";
    private const string Nonce = "server-issued-provider-nonce";

    private static ConsumerExternalAuthOptions Enabled() => new()
    {
        Google = new() { Enabled = true, ClientIds = [Client] },
        Apple = new() { Enabled = true, ClientIds = ["com.korridorx.korridorxMobile"] }
    };

    private static string Token(RSA rsa, string provider = "google", string nonce = Nonce, string? audience = null)
    {
        var claims = new[] { new Claim("sub", "stable-identity"), new Claim("nonce", nonce),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) };
        var token = new JwtSecurityToken(provider == "google" ? "https://accounts.google.com" : "https://appleid.apple.com",
            audience ?? (provider == "google" ? Client : "com.korridorx.korridorxMobile"), claims,
            DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ExternalProviderTokenVerifier Verifier(RSA rsa) => new(
        new ExternalAuthChallengeService(new EphemeralDataProtectionProvider(), Options.Create(Enabled())),
        new TestKeys(new RsaSecurityKey(rsa) { KeyId = "test-key" }));

    [Theory, InlineData("google"), InlineData("apple")]
    public async Task Signed_provider_credential_resolves_only_its_stable_subject(string provider)
    {
        using var rsa = RSA.Create(2048);
        var value = await Verifier(rsa).VerifyAsync(provider, Token(rsa, provider), Nonce, default);
        Assert.Equal(provider, value.Provider); Assert.Equal("stable-identity", value.Subject);
    }

    [Fact]
    public async Task Valid_claims_do_not_override_a_wrong_RSA_signature()
    {
        using var trusted = RSA.Create(2048); using var attacker = RSA.Create(2048);
        await Assert.ThrowsAsync<UnauthorizedApiException>(() => Verifier(trusted).VerifyAsync("google", Token(attacker), Nonce, default));
    }

    [Theory, InlineData("wrong-nonce", Client), InlineData(Nonce, "unowned.apps.googleusercontent.com")]
    public async Task Signature_alone_does_not_authorize_wrong_nonce_or_audience(string nonce, string audience)
    {
        using var rsa = RSA.Create(2048);
        await Assert.ThrowsAsync<UnauthorizedApiException>(() => Verifier(rsa).VerifyAsync("google", Token(rsa, nonce: nonce, audience: audience), Nonce, default));
    }

    [Fact]
    public async Task Unconfigured_provider_never_fetches_a_key_or_accepts_a_token()
    {
        using var rsa = RSA.Create(2048);
        var keys = new TestKeys(new RsaSecurityKey(rsa));
        var verifier = new ExternalProviderTokenVerifier(new ExternalAuthChallengeService(new EphemeralDataProtectionProvider(),
            Options.Create(new ConsumerExternalAuthOptions())), keys);
        await Assert.ThrowsAsync<ForbiddenException>(() => verifier.VerifyAsync("google", Token(rsa), Nonce, default));
        Assert.Equal(0, keys.Requests);
    }

    [Fact]
    public void Provider_configuration_is_disabled_by_default_and_exact_when_enabled()
    {
        var validator = new ConsumerExternalAuthOptionsValidator();
        Assert.True(validator.Validate(null, new()).Succeeded);
        var options = Enabled(); Assert.True(validator.Validate(null, options).Succeeded);
        options.Google.ClientIds = []; Assert.True(validator.Validate(null, options).Failed);
        options.Google.ClientIds = ["*.apps.googleusercontent.com"]; Assert.True(validator.Validate(null, options).Failed);
        options.Google.ClientIds = [" owned.apps.googleusercontent.com"]; Assert.True(validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Challenges_bind_provider_purpose_owner_and_nonce()
    {
        var service = new ExternalAuthChallengeService(new EphemeralDataProtectionProvider(), Options.Create(Enabled()));
        var owner = Guid.NewGuid();
        var signIn = service.Create("google", null);
        Assert.Equal(signIn.Nonce, service.Read(signIn.ChallengeToken, "google", null).Nonce);
        Assert.Throws<UnauthorizedApiException>(() => service.Read(signIn.ChallengeToken, "apple", null));
        Assert.Throws<UnauthorizedApiException>(() => service.Read(signIn.ChallengeToken, "google", owner));
        var link = service.Create("apple", owner);
        Assert.Equal("link", service.Read(link.ChallengeToken, "apple", owner).Purpose);
        Assert.Equal(64, link.Nonce.Length);
        Assert.Throws<UnauthorizedApiException>(() => service.Read(link.ChallengeToken, "apple", Guid.NewGuid()));
        Assert.Throws<UnauthorizedApiException>(() => service.Read(link.ChallengeToken, "apple", null));
        Assert.Throws<UnauthorizedApiException>(() => service.Read(link.ChallengeToken + "tampered", "apple", owner));
    }

    private sealed class TestKeys(SecurityKey key) : IExternalSigningKeySource
    {
        public int Requests { get; private set; }
        public Task<IReadOnlyList<SecurityKey>> GetAsync(string provider, string kid, CancellationToken ct)
        { Requests++; return Task.FromResult<IReadOnlyList<SecurityKey>>([key]); }
    }
}
