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

public sealed class BlaaizInteracReadTokenTests
{
    private const string ReadScope = "interac-auto-deposit-email:read";

    [Fact]
    public async Task Registration_reads_request_only_the_read_scope_and_never_reuse_or_replace_the_standard_token()
    {
        using var handler = new OAuthHandler();
        using var http = CreateHttp(handler);
        using var cache = new RecordingCache();
        cache.Set("Blaaiz:OAuthAccessToken", "standard-cached-token");
        var options = Settings();
        var audit = new RecordingAudit();
        var service = new BlaaizTokenService(new HttpFactory(http), cache, Options.Create(options), audit);

        Assert.Equal("standard-cached-token", await service.GetAccessTokenAsync());
        var first = await service.GetInteracAutoDepositReadAccessTokenAsync();
        var second = await service.GetInteracAutoDepositReadAccessTokenAsync();

        Assert.Equal("read-token-1", first);
        Assert.Equal(first, second);
        Assert.Equal("standard-cached-token", await service.GetAccessTokenAsync());
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://blaaiz.example.test/oauth/token", request.Uri);
        Assert.Equal("client_credentials", request.Form["grant_type"]);
        Assert.Equal(ReadScope, request.Form["scope"]);
        Assert.Equal(options.ClientId, request.Form["client_id"]);
        Assert.Equal(options.ClientSecret, request.Form["client_secret"]);
        Assert.Equal("transaction:read wallet:read payout:create", options.Scopes);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.DoesNotContain(options.ClientSecret, string.Join("\n", audit.Messages));
        Assert.DoesNotContain(first, string.Join("\n", audit.Messages));
        var scopedKey = Assert.Single(cache.Keys, key => key.StartsWith("Blaaiz:OAuth:InteracRead:", StringComparison.Ordinal));
        Assert.Matches("^Blaaiz:OAuth:InteracRead:[A-F0-9]{64}$", scopedKey);
        Assert.DoesNotContain(options.ClientId, scopedKey);
        Assert.DoesNotContain(options.ClientSecret, scopedKey);
    }

    [Theory]
    [InlineData("client-id")]
    [InlineData("client-secret")]
    [InlineData("base-url")]
    [InlineData("base-path")]
    [InlineData("token-endpoint")]
    public async Task Scoped_cache_does_not_cross_credentials_or_connection_endpoints(string changedPart)
    {
        using var handler = new OAuthHandler();
        using var http = CreateHttp(handler);
        using var cache = new RecordingCache();
        var options = Settings();
        var service = new BlaaizTokenService(new HttpFactory(http), cache, Options.Create(options), new RecordingAudit());

        var first = await service.GetInteracAutoDepositReadAccessTokenAsync();
        switch (changedPart)
        {
            case "client-id": options.ClientId = "client-beta"; break;
            case "client-secret": options.ClientSecret = "client-secret-beta"; break;
            case "base-url": options.BaseUrl = "https://another-blaaiz.example.test"; break;
            case "base-path": options.BaseUrl = "https://blaaiz.example.test/connection-b"; break;
            case "token-endpoint": options.TokenEndpoint = "/alternate/oauth/token"; break;
        }
        var second = await service.GetInteracAutoDepositReadAccessTokenAsync();
        var repeated = await service.GetInteracAutoDepositReadAccessTokenAsync();

        Assert.NotEqual(first, second);
        Assert.Equal(second, repeated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(options.ClientId, handler.Requests[1].Form["client_id"]);
        Assert.Equal(options.ClientSecret, handler.Requests[1].Form["client_secret"]);
        Assert.All(handler.Requests, request => Assert.Equal(ReadScope, request.Form["scope"]));
        Assert.Equal(2, cache.Keys.Distinct().Count());
        if (changedPart == "base-url")
            Assert.Equal("https://another-blaaiz.example.test/oauth/token", handler.Requests[1].Uri);
        if (changedPart == "token-endpoint")
            Assert.Equal("https://blaaiz.example.test/alternate/oauth/token", handler.Requests[1].Uri);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    public async Task Scope_authentication_failure_is_actionable_sanitized_and_never_falls_back_or_caches(int status)
    {
        const string unsafeBody = """{"error":"invalid_scope","error_description":"echo client-secret-alpha","access_token":"unexpected-token"}""";
        using var handler = new OAuthHandler((HttpStatusCode)status, unsafeBody);
        using var http = CreateHttp(handler);
        using var cache = new RecordingCache();
        var audit = new RecordingAudit();
        var service = new BlaaizTokenService(new HttpFactory(http), cache, Options.Create(Settings()), audit);

        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetInteracAutoDepositReadAccessTokenAsync());
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetInteracAutoDepositReadAccessTokenAsync());

        Assert.Equal(status, error.ProviderStatusCode);
        Assert.Contains(ReadScope, error.Message);
        Assert.DoesNotContain("client-secret-alpha", error.ProviderResponse!);
        Assert.DoesNotContain("unexpected-token", error.ProviderResponse!);
        Assert.DoesNotContain("client-secret-alpha", string.Join("\n", audit.Messages));
        Assert.DoesNotContain("unexpected-token", string.Join("\n", audit.Messages));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(ReadScope, request.Form["scope"]));
        Assert.Empty(cache.Keys);
        Assert.Equal(0, audit.CompleteCalls);
        Assert.Equal(2, audit.FailCalls);
    }

    [Theory]
    [InlineData("transaction:read", 3600, "Bearer", "token")]
    [InlineData("interac-auto-deposit-email:read payout:create", 3600, "Bearer", "token")]
    [InlineData("", 3600, "Bearer", "token")]
    [InlineData("interac-auto-deposit-email:read", 0, "Bearer", "token")]
    [InlineData("interac-auto-deposit-email:read", -1, "Bearer", "token")]
    [InlineData("interac-auto-deposit-email:read", 3600, "Basic", "token")]
    [InlineData("interac-auto-deposit-email:read", 3600, "Bearer", "")]
    public async Task Invalid_or_broader_OAuth_response_is_not_cached(string scope, int expiresIn, string tokenType, string accessToken)
    {
        var body = JsonSerializer.Serialize(new { token_type = tokenType, access_token = accessToken, expires_in = expiresIn, scope });
        using var handler = new OAuthHandler(HttpStatusCode.OK, body);
        using var http = CreateHttp(handler);
        using var cache = new RecordingCache();
        var audit = new RecordingAudit();
        var service = new BlaaizTokenService(new HttpFactory(http), cache, Options.Create(Settings()), audit);

        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetInteracAutoDepositReadAccessTokenAsync());

        Assert.Equal(200, error.ProviderStatusCode);
        Assert.Contains("invalid OAuth response", error.Message);
        Assert.Empty(cache.Keys);
        Assert.Equal(0, audit.CompleteCalls);
        Assert.Equal(1, audit.FailCalls);
    }

    [Theory]
    [InlineData("https://other.example.test/oauth/token")]
    [InlineData("http://blaaiz.example.test/oauth/token")]
    [InlineData("https://someone:secret@blaaiz.example.test/oauth/token")]
    public async Task Registration_read_authentication_does_not_send_credentials_to_a_different_connection(string tokenEndpoint)
    {
        using var handler = new OAuthHandler();
        using var http = CreateHttp(handler);
        using var cache = new RecordingCache();
        var options = Settings();
        options.TokenEndpoint = tokenEndpoint;
        var service = new BlaaizTokenService(new HttpFactory(http), cache, Options.Create(options), new RecordingAudit());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetInteracAutoDepositReadAccessTokenAsync());

        Assert.Empty(handler.Requests);
        Assert.Empty(cache.Keys);
    }

    [Fact]
    public async Task Registration_client_uses_narrow_token_while_transaction_confirmation_keeps_standard_token()
    {
        const string registrationId = "01994c21-a4d0-7000-8000-123456789abc";
        using var handler = new ReadApiHandler();
        using var http = CreateHttp(handler);
        var tokenService = new DistinctTokens();
        var client = new BlaaizApiClient(http, tokenService, new RecordingAudit());

        await client.GetInteracAutoDepositEmailAsync(registrationId);
        await client.ListInteracAutoDepositEmailsAsync();
        await client.GetTransactionAsync("fixture-transaction");

        Assert.Equal(2, tokenService.InteracReadCalls);
        Assert.Equal(1, tokenService.StandardCalls);
        Assert.Equal(new[] { "Bearer scoped-read-token", "Bearer scoped-read-token", "Bearer standard-token" }, handler.Authorizations);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
    }

    private static BlaaizOptions Settings() => new()
    {
        IsEnabled = true,
        BaseUrl = "https://blaaiz.example.test",
        TokenEndpoint = "/oauth/token",
        ClientId = "client-alpha",
        ClientSecret = "client-secret-alpha",
        Scopes = "transaction:read wallet:read payout:create",
        TokenRefreshBufferSeconds = 60
    };

    private static HttpClient CreateHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };

    private sealed class HttpFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("BlaaizAuth", name);
            return client;
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, Dictionary<string, string> Form);

    private sealed class OAuthHandler(HttpStatusCode status = HttpStatusCode.OK, string? responseBody = null) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            var form = body.Split('&').Select(part => part.Split('=', 2)).ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsoluteUri, form));
            var payload = responseBody ?? JsonSerializer.Serialize(new { token_type = "Bearer", expires_in = 3600, access_token = "read-token-" + Requests.Count, scope = ReadScope });
            return new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ReadApiHandler : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];
        public List<HttpMethod> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            Methods.Add(request.Method);
            var body = request.RequestUri!.PathAndQuery.Contains('?') ? "{\"data\":[]}" : "{\"data\":{}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class DistinctTokens : IBlaaizTokenService
    {
        public int StandardCalls { get; private set; }
        public int InteracReadCalls { get; private set; }
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            StandardCalls++;
            return Task.FromResult("standard-token");
        }
        public Task<string> GetInteracAutoDepositReadAccessTokenAsync(CancellationToken ct = default)
        {
            InteracReadCalls++;
            return Task.FromResult("scoped-read-token");
        }
    }

    private sealed class RecordingCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        public List<string> Keys { get; } = [];
        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
        public ICacheEntry CreateEntry(object key)
        {
            Keys.Add(key.ToString()!);
            return _inner.CreateEntry(key);
        }
        public void Remove(object key) => _inner.Remove(key);
        public void Dispose() => _inner.Dispose();
    }

    private sealed class RecordingAudit : IProviderRequestAuditService
    {
        public List<string> Messages { get; } = [];
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default)
        {
            Messages.Add(requestBodyJson ?? "");
            return Task.FromResult(Guid.NewGuid());
        }
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson, long durationMs, CancellationToken ct = default)
        {
            CompleteCalls++;
            Messages.Add(responseBodyJson ?? "");
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson, string errorMessage, long durationMs, CancellationToken ct = default)
        {
            FailCalls++;
            Messages.Add(responseBodyJson ?? "");
            Messages.Add(errorMessage);
            return Task.CompletedTask;
        }
    }
}
