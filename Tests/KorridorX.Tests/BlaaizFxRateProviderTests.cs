using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Fx;
using KorridorX.Services.Providers;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizFxRateProviderTests
{
    [Theory]
    [InlineData("1100.25")]
    [InlineData("\"1100.25\"")]
    public void Parser_keeps_exact_direction_value_flags_and_provider_time(string value)
    {
        var parsed = BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(value)), " cad ", "ngn");
        Assert.Equal("CAD", parsed.SourceCurrencyCode);
        Assert.Equal("NGN", parsed.DestinationCurrencyCode);
        Assert.Equal(1100.25m, parsed.Rate);
        Assert.Equal("12", parsed.ProviderRateId);
        Assert.Equal(new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc), parsed.ProviderTimestampUtc);
        Assert.True(parsed.SendEnabled);
        Assert.True(parsed.SwapEnabled);
    }

    [Fact]
    public void Parser_never_inverts_reverse_or_accepts_partial_pairs()
    {
        var json = FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(pair: "NGN/CAD"), FxProviderTestSupport.Row(pair: "XCAD/NGN"));
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(json, "CAD", "NGN"));
    }

    [Fact]
    public void Multiple_eligible_rows_require_exact_provider_id_without_category_guessing()
    {
        var json = FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(id: "12"), FxProviderTestSupport.Row("\"1090\"", id: "13"));
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(json, "CAD", "NGN"));
        Assert.Equal(1090m, BlaaizFxRateParser.Parse(json, "CAD", "NGN", "13").Rate);
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(json, "CAD", "NGN", "14"));
    }

    [Fact]
    public void Duplicate_provider_id_remains_ambiguous()
    {
        var row = FxProviderTestSupport.Row();
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(row, row), "CAD", "NGN", "12"));
    }

    [Fact]
    public void Configured_id_does_not_fall_back_to_another_active_row()
    {
        var json = FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(id: "12", active: "false"), FxProviderTestSupport.Row(id: "13"));
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(json, "CAD", "NGN", "12"));
    }

    [Theory]
    [InlineData("false", "true", false, true)]
    [InlineData("true", "false", true, false)]
    [InlineData("null", "true", false, true)]
    public void Capability_flags_are_preserved_for_the_callers_operation_policy(string send, string swap, bool expectedSend, bool expectedSwap)
    {
        var parsed = BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(send: send, swap: swap)), "CAD", "NGN");
        Assert.Equal(expectedSend, parsed.SendEnabled);
        Assert.Equal(expectedSwap, parsed.SwapEnabled);
    }

    [Theory]
    [InlineData("false", "true", "true")]
    [InlineData("null", "true", "true")]
    [InlineData("true", "false", "false")]
    [InlineData("true", "null", "null")]
    [InlineData("true", "\"true\"", "false")]
    public void Inactive_unusable_or_malformed_capabilities_fail_closed(string active, string send, string swap)
    {
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(
            FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(active: active, send: send, swap: swap)), "CAD", "NGN"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"NaN\"")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"1,100.25\"")]
    [InlineData("1e100")]
    public void Invalid_rate_values_are_rejected(string value)
    {
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(value)), "CAD", "NGN"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":[null]}")]
    [InlineData("not-json")]
    public void Missing_or_incompatible_response_does_not_fabricate_a_rate(string json)
    {
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(json, "CAD", "NGN"));
    }

    [Fact]
    public void Missing_provider_timestamp_stays_unknown_and_created_at_is_not_substituted()
    {
        var row = FxProviderTestSupport.Row(updatedAt: "null").Replace("\"updated_at\":null", "\"updated_at\":null,\"created_at\":\"2026-10-01T00:00:00Z\"");
        Assert.Null(BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(row), "CAD", "NGN").ProviderTimestampUtc);
    }

    [Theory]
    [InlineData("\"2026-10-20T08:00:00\"")]
    [InlineData("\"not-a-date\"")]
    public void Unqualified_or_invalid_provider_timestamp_is_not_replaced_with_now(string timestamp)
    {
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(updatedAt: timestamp)), "CAD", "NGN"));
    }

    [Fact]
    public void Duplicate_rate_fields_are_rejected()
    {
        var row = FxProviderTestSupport.Row().Replace("\"value\":", "\"value\":\"1\",\"value\":");
        Assert.Throws<ProviderIntegrationException>(() => BlaaizFxRateParser.Parse(FxProviderTestSupport.Envelope(row), "CAD", "NGN"));
    }

    [Fact]
    public async Task Each_sequential_read_uses_GET_and_does_not_reuse_the_previous_rate()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var tokens = new FxProviderTestSupport.Tokens();
        var audit = new FxProviderTestSupport.Audit();
        var clock = new FxProviderTestSupport.Clock();
        var provider = FxProviderTestSupport.Provider(http, tokens, audit, clock: clock);

        var first = await provider.GetRateAsync("cad", "ngn");
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = await provider.GetRateAsync("CAD", "NGN");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, tokens.Calls);
        Assert.NotEqual(first.RequestLogId, second.RequestLogId);
        Assert.Equal(first.ProviderTimestampUtc, second.ProviderTimestampUtc);
        Assert.True(second.RequestedAtUtc > first.RequestedAtUtc);
        Assert.Equal(provider.ConnectionKey, second.ConnectionKey);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Equal("https://api.example.test/api/external/rate?search_term=CAD%2FNGN", request.Uri);
            Assert.Equal("Bearer fixture-rate-token", request.Authorization);
            Assert.Null(request.Body);
            Assert.Contains("KorridorX/1.0", request.UserAgent);
            Assert.Contains("no-cache", request.CacheControl);
            Assert.False(request.HasLegacyApiKey);
        });
        Assert.Equal(2, audit.Completed);
        Assert.Equal(0, audit.Failed);
        Assert.All(audit.Headers, value => Assert.DoesNotContain("fixture-rate-token", value));
    }

    [Fact]
    public async Task A_later_provider_failure_cannot_return_the_previous_successful_rate()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);
        _ = await provider.GetRateAsync("CAD", "NGN");
        handler.Status = HttpStatusCode.ServiceUnavailable;
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(503, error.ProviderStatusCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(1, audit.Completed);
        Assert.Equal(1, audit.Failed);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Read_failures_preserve_status_and_redact_bearer_from_audits(int status)
    {
        using var handler = new FxProviderTestSupport.Handler("{\"message\":\"fixture-rate-token\"}") { Status = (HttpStatusCode)status };
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(status, error.ProviderStatusCode);
        Assert.NotNull(error.RequestLogId);
        Assert.DoesNotContain("fixture-rate-token", error.ToString());
        Assert.DoesNotContain("fixture-rate-token", audit.LastResponse);
        Assert.Equal(1, audit.Failed);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("ambiguous")]
    public async Task Semantically_invalid_success_is_audited_as_failure(string errorKind)
    {
        var body = errorKind switch
        {
            "ambiguous" => FxProviderTestSupport.Envelope(FxProviderTestSupport.Row(), FxProviderTestSupport.Row(id: "13")),
            "malformed" => "not-json",
            _ => "{\"data\":[]}"
        };
        using var handler = new FxProviderTestSupport.Handler(body);
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(200, error.ProviderStatusCode);
        Assert.Equal(0, audit.Completed);
        Assert.Equal(1, audit.Failed);
    }

    [Fact]
    public async Task Deadline_covers_response_body_after_HTTP_headers_and_finishes_failed_audit()
    {
        using var stalled = new FxProviderTestSupport.StalledStream();
        using var handler = new FxProviderTestSupport.Handler("")
        { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stalled) }) };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var options = FxProviderTestSupport.Options();
        options.TimeoutSeconds = 1;
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit, options);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("timed out", error.Message);
        Assert.Equal(200, error.ProviderStatusCode);
        Assert.Equal(0, audit.Completed);
        Assert.Equal(1, audit.Failed);
        Assert.True(stalled.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task Caller_cancellation_while_reading_body_remains_cancellation_and_closes_audit()
    {
        using var stalled = new FxProviderTestSupport.StalledStream();
        using var handler = new FxProviderTestSupport.Handler("")
        { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stalled) }) };
        using var http = new HttpClient(handler);
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), audit);
        using var cancellation = new CancellationTokenSource();
        var reading = provider.GetRateAsync("CAD", "NGN", ct: cancellation.Token);
        await stalled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        Assert.Equal(1, audit.Failed);
        Assert.Equal(0, audit.Completed);
    }

    [Fact]
    public async Task Deadline_includes_waiting_for_authentication_before_the_rate_request()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var options = FxProviderTestSupport.Options();
        options.TimeoutSeconds = 1;
        var tokens = new FxProviderTestSupport.Tokens
        { ReadAsync = async ct => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return "unused"; } };
        var audit = new FxProviderTestSupport.Audit();
        var provider = FxProviderTestSupport.Provider(http, tokens, audit, options);
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("timed out", error.Message);
        Assert.Empty(handler.Requests);
        Assert.Empty(audit.Headers);
    }

    [Fact]
    public async Task Invalid_pair_or_rate_id_fails_before_token_or_HTTP()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var tokens = new FxProviderTestSupport.Tokens();
        var provider = FxProviderTestSupport.Provider(http, tokens, new FxProviderTestSupport.Audit());
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRateAsync("CAD/NGN", "USD"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRateAsync("CAD", "CAD"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRateAsync("CAD", "NGN", "../123"));
        Assert.Equal(0, tokens.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Token_connection_mismatch_fails_before_authentication_and_HTTP()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var tokens = new FxProviderTestSupport.Tokens();
        var provider = FxProviderTestSupport.Provider(http, tokens, new FxProviderTestSupport.Audit());
        tokens.ConnectionKey = new string('0', 64);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(0, tokens.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Token_connection_change_while_authentication_is_in_progress_cannot_make_rate_request()
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var tokens = new FxProviderTestSupport.Tokens();
        var provider = FxProviderTestSupport.Provider(http, tokens, new FxProviderTestSupport.Audit());
        tokens.OnRead = () => tokens.ConnectionKey = new string('0', 64);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(1, tokens.Calls);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("missing-client")]
    [InlineData("missing-secret")]
    [InlineData("http-api")]
    [InlineData("foreign-token-host")]
    [InlineData("userinfo")]
    public async Task Availability_is_local_and_invalid_connections_never_call_provider(string mutation)
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var options = FxProviderTestSupport.Options();
        switch (mutation)
        {
            case "disabled": options.IsEnabled = false; break;
            case "missing-client": options.ClientId = ""; break;
            case "missing-secret": options.ClientSecret = ""; break;
            case "http-api": options.BaseUrl = "http://api.example.test"; break;
            case "foreign-token-host": options.TokenEndpoint = "https://other.example.test/oauth/token"; break;
            case "userinfo": options.BaseUrl = "https://secret@api.example.test"; break;
        }
        var tokens = new FxProviderTestSupport.Tokens();
        var provider = FxProviderTestSupport.Provider(http, tokens, new FxProviderTestSupport.Audit(), options);
        Assert.False(provider.IsAvailable);
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => provider.GetRateAsync("CAD", "NGN"));
        Assert.Equal(0, tokens.Calls);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("client")]
    [InlineData("api")]
    [InlineData("token")]
    [InlineData("environment")]
    public void Connection_fingerprint_changes_for_every_authentication_boundary(string changed)
    {
        using var handler = new FxProviderTestSupport.Handler(FxProviderTestSupport.ValidJson);
        using var http = new HttpClient(handler);
        var original = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), new FxProviderTestSupport.Audit());
        var options = FxProviderTestSupport.Options();
        var environment = "Testing";
        switch (changed)
        {
            case "secret": options.ClientSecret = "rotated-fixture-secret"; break;
            case "client": options.ClientId = "other-fixture-client"; break;
            case "api": options.BaseUrl = "https://other.example.test"; break;
            case "token": options.TokenEndpoint = "/oauth/token-v2"; break;
            case "environment": environment = "Staging"; break;
        }
        var current = FxProviderTestSupport.Provider(http, new FxProviderTestSupport.Tokens(), new FxProviderTestSupport.Audit(), options, environment);
        Assert.NotEqual(original.ConnectionKey, current.ConnectionKey);
        Assert.Matches("^[A-F0-9]{64}$", current.ConnectionKey);
    }
}

internal static class FxProviderTestSupport
{
    public static string ValidJson => Envelope(Row());
    public static string Envelope(params string[] rows) => "{\"data\":[" + string.Join(',', rows) + "]}";
    public static string Row(string value = "\"1100.25\"", string pair = "CAD/NGN", string id = "12",
        string active = "true", string send = "true", string swap = "true", string updatedAt = "\"2026-10-20T08:00:00+02:00\"") =>
        "{\"id\":" + id + ",\"pair\":" + JsonSerializer.Serialize(pair) + ",\"value\":" + value +
        ",\"is_active\":" + active + ",\"send_enabled\":" + send + ",\"swap_enabled\":" + swap +
        ",\"updated_at\":" + updatedAt + ",\"category\":\"SINGLE_BUSINESS\",\"type\":\"FLAT\"}";
    public static BlaaizOptions Options() => new()
    { IsEnabled = true, BaseUrl = "https://api.example.test", TokenEndpoint = "/oauth/token", ClientId = "fixture-client", ClientSecret = "fixture-secret", TokenRefreshBufferSeconds = 60 };
    public static BlaaizFxRateProvider Provider(HttpClient http, Tokens tokens, Audit audit, BlaaizOptions? options = null,
        string environment = "Testing", TimeProvider? clock = null)
    {
        var provider = new BlaaizFxRateProvider(http, tokens, audit, Microsoft.Extensions.Options.Options.Create(options ?? Options()), new Environment(environment), clock);
        tokens.ConnectionKey = provider.ConnectionKey;
        return provider;
    }

    internal sealed class Environment(string name = "Testing") : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "KorridorX.Fx.Tests";
        public string ContentRootPath { get; set; } = "/fixture";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }

    internal sealed class Tokens : IBlaaizFxRateTokenService
    {
        public string ConnectionKey { get; set; } = "";
        public int Calls { get; private set; }
        public Action? OnRead { get; set; }
        public Func<CancellationToken, Task<string>>? ReadAsync { get; set; }
        public Task<string> GetRatesReadAccessTokenAsync(CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Calls++; OnRead?.Invoke(); return ReadAsync?.Invoke(ct) ?? Task.FromResult("fixture-rate-token"); }
    }

    internal sealed record ObservedRequest(string Method, string Uri, string? Authorization, string UserAgent,
        string? Body, string? ContentType, string CacheControl, bool HasLegacyApiKey);

    internal sealed class Handler(string body) : HttpMessageHandler
    {
        public string Body { get; set; } = body;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<ObservedRequest> Requests { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new ObservedRequest(request.Method.Method, request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization?.ToString(), request.Headers.UserAgent.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct), request.Content?.Headers.ContentType?.ToString(),
                request.Headers.CacheControl?.ToString() ?? "", request.Headers.Contains("x-blaaiz-api-key")));
            return Respond is null ? new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") } : await Respond(request, ct);
        }
    }

    internal sealed class Audit : IProviderRequestAuditService
    {
        public int Completed { get; private set; }
        public int Failed { get; private set; }
        public List<string> Headers { get; } = [];
        public string LastResponse { get; private set; } = "";
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod, string? requestHeadersJson,
            string? requestBodyJson, Guid? relatedTransferId = null, Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Headers.Add(requestHeadersJson ?? ""); return Task.FromResult(Guid.NewGuid()); }
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson, long durationMs, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Completed++; LastResponse = responseBodyJson ?? ""; return Task.CompletedTask; }
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson, string errorMessage, long durationMs, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Failed++; LastResponse = responseBodyJson ?? ""; return Task.CompletedTask; }
    }

    internal sealed class StalledStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
