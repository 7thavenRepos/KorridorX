using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace KorridorX.Tests;

public sealed class ConsumerExternalKeySourceTests
{
    private static string Jwks()
    {
        using var rsa = RSA.Create(2048); var key = rsa.ExportParameters(false);
        return JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = "known-key",
            n = WebEncoders.Base64UrlEncode(key.Modulus!), e = WebEncoders.Base64UrlEncode(key.Exponent!) } } });
    }

    [Fact]
    public async Task Key_fetch_is_pinned_and_valid_keys_are_cached()
    {
        using var handler = new Handler(Jwks()); using var client = new HttpClient(handler);
        using var source = new ExternalSigningKeySource(new Factory(client));
        Assert.Single(await source.GetAsync("google", "known-key", default));
        Assert.Single(await source.GetAsync("google", "known-key", default));
        Assert.Equal(1, handler.Requests);
        Assert.Equal("https://www.googleapis.com/oauth2/v3/certs", handler.LastUrl);
    }

    [Fact]
    public async Task Apple_uses_its_own_fixed_key_endpoint()
    {
        using var handler = new Handler(Jwks()); using var client = new HttpClient(handler);
        using var source = new ExternalSigningKeySource(new Factory(client));
        Assert.Single(await source.GetAsync("apple", "known-key", default));
        Assert.Equal("https://appleid.apple.com/auth/keys", handler.LastUrl);
    }

    [Fact]
    public async Task Unknown_kid_is_rejected_and_cannot_force_repeated_fetches()
    {
        using var handler = new Handler(Jwks()); using var client = new HttpClient(handler);
        using var source = new ExternalSigningKeySource(new Factory(client));
        await Assert.ThrowsAsync<UnauthorizedApiException>(() => source.GetAsync("google", "unknown-1", default));
        await Assert.ThrowsAsync<UnauthorizedApiException>(() => source.GetAsync("google", "unknown-2", default));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Key_server_failure_is_safe_and_does_not_accept_a_credential()
    {
        using var handler = new Handler("private provider error", HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler); using var source = new ExternalSigningKeySource(new Factory(client));
        var exception = await Assert.ThrowsAsync<ExternalIdentityProviderUnavailableException>(() => source.GetAsync("google", "known-key", default));
        Assert.DoesNotContain("private provider error", exception.Message);
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => client; }
    private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastUrl { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++; LastUrl = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
