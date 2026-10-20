using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Providers.Fx;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizFxRateTokenTests
{
    private const string ValidToken = """{"access_token":"fixture-scoped-token","token_type":"Bearer","expires_in":3600,"scope":"rates:read"}""";

    [Fact]
    public async Task OAuth_request_uses_only_rates_read_scope_and_original_configured_credentials()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new Factory(handler);
        using var service = Create(factory, cache);
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());

        var request = Assert.Single(handler.Requests);
        var form = ParseForm(request.Body!);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.example.test/oauth/token", request.Uri);
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
        Assert.Equal(4, form.Count);
        Assert.Equal("client_credentials", form["grant_type"]);
        Assert.Equal("fixture-client", form["client_id"]);
        Assert.Equal("fixture-secret", form["client_secret"]);
        Assert.Equal("rates:read", form["scope"]);
        Assert.Null(request.Authorization);
        Assert.False(request.HasLegacyApiKey);
        Assert.Contains("KorridorX/1.0", request.UserAgent);
        Assert.Equal(BlaaizFxRateTokenService.HttpClientName, Assert.Single(factory.Names));
        Assert.DoesNotContain("fixture-client", service.ConnectionKey);
        Assert.DoesNotContain("fixture-secret", service.ConnectionKey);
    }

    [Fact]
    public async Task Provider_and_scoped_token_share_exact_connection_identity()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var tokens = Create(new Factory(handler), cache);
        using var rateHandler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var rateHttp = new HttpClient(rateHandler);
        var provider = new BlaaizFxRateProvider(rateHttp, tokens, new FxProviderTestSupport.Audit(),
            Options.Create(FxProviderTestSupport.Options()), new FxProviderTestSupport.Environment());
        Assert.Equal(provider.ConnectionKey, tokens.ConnectionKey);
        Assert.Equal(1100.25m, (await provider.GetRateAsync("CAD", "NGN")).Rate);
        Assert.Single(handler.Requests);
        Assert.Single(rateHandler.Requests);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("client")]
    [InlineData("api")]
    [InlineData("token")]
    [InlineData("environment")]
    public async Task Cached_token_cannot_cross_provider_connection_boundaries(string change)
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new Factory(handler);
        using var original = Create(factory, cache);
        _ = await original.GetRatesReadAccessTokenAsync();
        var options = FxProviderTestSupport.Options();
        var environment = "Testing";
        switch (change)
        {
            case "secret": options.ClientSecret = "rotated-fixture-secret"; break;
            case "client": options.ClientId = "other-fixture-client"; break;
            case "api": options.BaseUrl = "https://different.example.test"; break;
            case "token": options.TokenEndpoint = "/oauth/another-token"; break;
            case "environment": environment = "Staging"; break;
        }
        using var changed = Create(factory, cache, options, environment);
        _ = await changed.GetRatesReadAccessTokenAsync();
        Assert.NotEqual(original.ConnectionKey, changed.ConnectionKey);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Refresh_buffer_replaces_token_before_its_provider_expiration()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken.Replace("3600", "120"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new FxProviderTestSupport.Clock();
        using var service = Create(new Factory(handler), cache, clock: clock);
        _ = await service.GetRatesReadAccessTokenAsync();
        clock.Advance(TimeSpan.FromSeconds(59));
        _ = await service.GetRatesReadAccessTokenAsync();
        Assert.Single(handler.Requests);
        clock.Advance(TimeSpan.FromSeconds(1));
        _ = await service.GetRatesReadAccessTokenAsync();
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Short_lived_token_is_used_once_without_extending_its_cache_lifetime()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken.Replace("3600", "30"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        _ = await service.GetRatesReadAccessTokenAsync();
        _ = await service.GetRatesReadAccessTokenAsync();
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Overlapping_authentication_refreshes_share_one_scoped_token_request()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FxProviderTestSupport.Handler(ValidToken)
        {
            Respond = async (_, ct) =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidToken, Encoding.UTF8, "application/json") };
            }
        };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        var first = service.GetRatesReadAccessTokenAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.GetRatesReadAccessTokenAsync();
        release.SetResult();
        Assert.Equal(["fixture-scoped-token", "fixture-scoped-token"], await Task.WhenAll(first, second));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task OAuth_deadline_covers_body_read_and_releases_refresh_permit_after_failure()
    {
        using var stalled = new FxProviderTestSupport.StalledStream();
        using var handler = new FxProviderTestSupport.Handler(ValidToken)
        { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stalled) }) };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = FxProviderTestSupport.Options();
        options.TimeoutSeconds = 1;
        using var service = Create(new Factory(handler), cache, options);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetRatesReadAccessTokenAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("timed out", error.Message);
        Assert.True(stalled.Started.Task.IsCompleted);
        handler.Respond = null;
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task OAuth_deadline_bounds_wait_for_an_existing_refresh_permit()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FxProviderTestSupport.Handler(ValidToken)
        {
            Respond = async (_, _) =>
            {
                entered.TrySetResult();
                // Deliberately simulate a handler that ignores cancellation before returning headers.
                await release.Task;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidToken) };
            }
        };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = FxProviderTestSupport.Options();
        options.TimeoutSeconds = 1;
        using var service = Create(new Factory(handler), cache, options);
        var first = service.GetRatesReadAccessTokenAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetRatesReadAccessTokenAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("timed out", error.Message);
            Assert.Single(handler.Requests);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Caller_cancellation_during_OAuth_body_read_is_not_reported_as_provider_failure()
    {
        using var stalled = new FxProviderTestSupport.StalledStream();
        using var handler = new FxProviderTestSupport.Handler(ValidToken)
        { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stalled) }) };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        using var cancellation = new CancellationTokenSource();
        var first = service.GetRatesReadAccessTokenAsync(cancellation.Token);
        await stalled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        handler.Respond = null;
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Basic\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"\",\"token_type\":\"Bearer\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"bad token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":0}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":-1}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":\"3600\"}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"wallet:read\"}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"rates:read payout:create\"}")]
    [InlineData("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"rates:read\",\"scope\":\"payout:create\"}")]
    public async Task Invalid_token_response_is_never_cached_or_replaced_by_broader_authentication(string body)
    {
        using var handler = new FxProviderTestSupport.Handler(body);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetRatesReadAccessTokenAsync());
        handler.Body = ValidToken;
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("rates:read", ParseForm(request.Body!)["scope"]));
    }

    [Fact]
    public async Task Omitted_scope_response_keeps_the_explicitly_requested_OAuth_scope()
    {
        using var handler = new FxProviderTestSupport.Handler("""{"access_token":"fixture-scoped-token","token_type":"Bearer","expires_in":3600}""");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        Assert.Equal("fixture-scoped-token", await service.GetRatesReadAccessTokenAsync());
        Assert.Equal("rates:read", ParseForm(Assert.Single(handler.Requests).Body!)["scope"]);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task OAuth_errors_keep_HTTP_status_without_exposing_credentials_or_body(int status)
    {
        using var handler = new FxProviderTestSupport.Handler("""{"client_secret":"fixture-secret","access_token":"fixture-leaked-token"}""")
        { Status = (HttpStatusCode)status };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetRatesReadAccessTokenAsync());
        Assert.Equal(status, error.ProviderStatusCode);
        Assert.Null(error.ProviderResponse);
        Assert.DoesNotContain("fixture-secret", error.ToString());
        Assert.DoesNotContain("fixture-leaked-token", error.ToString());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Caller_cancellation_never_starts_authentication()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = Create(new Factory(handler), cache);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetRatesReadAccessTokenAsync(cancellation.Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Disabled_authentication_never_opens_an_HTTP_client()
    {
        using var handler = new FxProviderTestSupport.Handler(ValidToken);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new Factory(handler);
        var options = FxProviderTestSupport.Options();
        options.IsEnabled = false;
        using var service = Create(factory, cache, options);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.GetRatesReadAccessTokenAsync());
        Assert.Empty(factory.Names);
        Assert.Empty(handler.Requests);
    }

    private static BlaaizFxRateTokenService Create(Factory factory, IMemoryCache cache, BlaaizOptions? options = null,
        string environment = "Testing", TimeProvider? clock = null) =>
        new(factory, cache, Options.Create(options ?? FxProviderTestSupport.Options()), new FxProviderTestSupport.Environment(environment), clock);

    private static Dictionary<string, string> ParseForm(string body) => body.Split('&').Select(x => x.Split('=', 2))
        .ToDictionary(x => Uri.UnescapeDataString(x[0].Replace('+', ' ')), x => Uri.UnescapeDataString(x[1].Replace('+', ' ')), StringComparer.Ordinal);

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public List<string> Names { get; } = [];
        public HttpClient CreateClient(string name)
        { Names.Add(name); return new HttpClient(handler, disposeHandler: false); }
    }
}
