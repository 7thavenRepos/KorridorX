using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizWithdrawalFeeReadTokenTests
{
    [Fact]
    public async Task Fee_quote_uses_only_fees_read_and_never_changes_the_standard_payout_scopes()
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("Blaaiz:OAuthAccessToken", "existing-payout-token");
        var options = Settings();
        var service = new BlaaizTokenService(new Factory(http), cache, Options.Create(options), new Audit());

        var first = await service.GetFeeReadAccessTokenAsync();
        var second = await service.GetFeeReadAccessTokenAsync();

        Assert.Equal("fee-read-token", first);
        Assert.Equal(first, second);
        Assert.Equal("existing-payout-token", await service.GetAccessTokenAsync());
        Assert.Equal(1, handler.Calls);
        Assert.Equal("https://blaaiz.example.test/oauth/token", handler.Uri);
        Assert.Equal("fees:read", handler.Form["scope"]);
        Assert.Equal("client_credentials", handler.Form["grant_type"]);
        Assert.Equal(options.ClientId, handler.Form["client_id"]);
        Assert.Equal(options.ClientSecret, handler.Form["client_secret"]);
        Assert.Equal("transaction:read payout:create", options.Scopes);
    }

    [Fact]
    public async Task Fee_scope_cache_rotates_with_oauth_connection_credentials()
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Settings();
        var service = new BlaaizTokenService(new Factory(http), cache, Options.Create(options), new Audit());
        await service.GetFeeReadAccessTokenAsync();
        options.ClientSecret = "different-secret";
        await service.GetFeeReadAccessTokenAsync();
        Assert.Equal(2, handler.Calls);
        Assert.Equal("different-secret", handler.Form["client_secret"]);
    }

    [Theory]
    [InlineData("payout:create fees:read")]
    [InlineData("transaction:read")]
    public async Task Broader_or_incorrect_scopes_fail_closed_without_token_caching(string grantedScope)
    {
        using var handler = new TokenHandler(grantedScope);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new BlaaizTokenService(new Factory(http), cache, Options.Create(Settings()), new Audit());
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetFeeReadAccessTokenAsync());
        Assert.Contains("invalid OAuth response", error.Message);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetFeeReadAccessTokenAsync());
        Assert.Equal(2, handler.Calls);
    }

    private static BlaaizOptions Settings() => new()
    {
        IsEnabled = true, BaseUrl = "https://blaaiz.example.test", TokenEndpoint = "/oauth/token",
        ClientId = "fee-read-client", ClientSecret = "fee-read-secret", Scopes = "transaction:read payout:create"
    };

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("BlaaizAuth", name);
            return client;
        }
    }

    private sealed class TokenHandler(string returnedScope = "fees:read") : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Uri { get; private set; }
        public Dictionary<string, string> Form { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Uri = request.RequestUri!.AbsoluteUri;
            var body = await request.Content!.ReadAsStringAsync(ct);
            Form = body.Split('&').Select(item => item.Split('=', 2)).ToDictionary(
                part => System.Uri.UnescapeDataString(part[0].Replace('+', ' ')),
                part => System.Uri.UnescapeDataString(part[1].Replace('+', ' ')));
            var json = JsonSerializer.Serialize(new { token_type = "Bearer", expires_in = 3600,
                access_token = "fee-read-token", scope = returnedScope });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Audit : IProviderRequestAuditService
    {
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default) =>
            Task.FromResult(Guid.NewGuid());
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default) => Task.CompletedTask;
    }
}
