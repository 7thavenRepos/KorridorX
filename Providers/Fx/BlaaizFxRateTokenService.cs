using System.Net.Http.Headers;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Fx;

// A separate OAuth read boundary: no change to existing collection/payout/Interac tokens or scopes.
// https://docs.blaaiz.com/docs/guides/authentication
public sealed class BlaaizFxRateTokenService : IBlaaizFxRateTokenService, IDisposable
{
    public const string HttpClientName = "BlaaizFxRatesAuth";
    public const string RequiredScope = "rates:read";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly BlaaizFxConnection _connection;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly string _cacheKey;

    public BlaaizFxRateTokenService(IHttpClientFactory httpClientFactory, IMemoryCache cache,
        IOptions<BlaaizOptions> options, IHostEnvironment environment, TimeProvider? clock = null)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _connection = new BlaaizFxConnection(options.Value, environment.EnvironmentName);
        _clock = clock ?? TimeProvider.System;
        _cacheKey = "KorridorX.Blaaiz.Fx.OAuth.v1:" + _connection.Key + ":" + RequiredScope;
    }

    public string ConnectionKey => _connection.Key;

    public async Task<string> GetRatesReadAccessTokenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _connection.EnsureAvailable();
        if (TryCached(out var cached)) return cached;
        var timeoutDuration = TimeSpan.FromSeconds(_connection.TimeoutSeconds);
        var startedAt = _clock.GetTimestamp();
        using var timeout = new CancellationTokenSource(timeoutDuration, _clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        var operationCt = deadline.Token;
        void CheckDeadline()
        {
            ct.ThrowIfCancellationRequested();
            // Timers can be dispatched late and custom transports can ignore cancellation.
            // Never accept/cache a result outside the original budget, including permit wait.
            if (operationCt.IsCancellationRequested || _clock.GetElapsedTime(startedAt) >= timeoutDuration)
                throw new OperationCanceledException(operationCt);
        }
        var entered = false;
        try
        {
            await _refresh.WaitAsync(operationCt);
            entered = true;
            CheckDeadline();
            if (TryCached(out cached)) return cached;
            var requestedAt = _clock.GetUtcNow();
            using var request = new HttpRequestMessage(HttpMethod.Post, _connection.TokenUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("KorridorX/1.0");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _connection.ClientId,
                ["client_secret"] = _connection.ClientSecret,
                ["scope"] = RequiredScope
            });
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationCt);
            CheckDeadline();
            if (!response.IsSuccessStatusCode)
                throw new ProviderIntegrationException(
                    "Blaaiz rejected the rates:read OAuth request. Check the current provider credentials, scope grant and API/IP access.",
                    (int)response.StatusCode);

            // Tokens and OAuth response bodies are deliberately never put in provider audit logs or exceptions.
            var json = await ReadBoundedAsync(response.Content, 64 * 1024, operationCt);
            CheckDeadline();
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1) ||
                !root.TryGetProperty("access_token", out var tokenElement) || tokenElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("token_type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String ||
                !string.Equals(typeElement.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("expires_in", out var expiryElement) || !expiryElement.TryGetInt32(out var expiresIn) || expiresIn <= 0)
                throw InvalidToken();
            var token = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 16384 || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl))
                throw InvalidToken();
            if (root.TryGetProperty("scope", out var scopeElement))
            {
                if (scopeElement.ValueKind != JsonValueKind.String ||
                    !(scopeElement.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
                        .SetEquals([RequiredScope]))
                    throw new ProviderIntegrationException("Blaaiz returned an OAuth token with an incompatible rates:read scope.");
            }
            var expiresAt = requestedAt.AddSeconds(expiresIn);
            if (expiresAt <= _clock.GetUtcNow()) throw InvalidToken();
            var usableUntil = expiresAt.AddSeconds(-_connection.RefreshBufferSeconds);
            CheckDeadline();
            if (usableUntil > _clock.GetUtcNow())
                _cache.Set(_cacheKey, new CachedToken(token, expiresAt), usableUntil);
            return token;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            throw new ProviderIntegrationException("The Blaaiz rates:read OAuth request timed out.");
        }
        catch (ProviderIntegrationException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or
                                         JsonException or InvalidOperationException or ArgumentException or IOException)
        {
            // Do not propagate a response body or transport exception that may contain OAuth material.
            throw new ProviderIntegrationException("Unable to obtain a valid Blaaiz rates:read OAuth token.");
        }
        finally { if (entered) _refresh.Release(); }
    }

    private bool TryCached(out string token)
    {
        if (_cache.TryGetValue<CachedToken>(_cacheKey, out var entry) && entry is not null &&
            entry.ExpiresAt > _clock.GetUtcNow().AddSeconds(_connection.RefreshBufferSeconds))
        {
            token = entry.Token;
            return true;
        }
        token = "";
        return false;
    }

    internal static async Task<string> ReadBoundedAsync(HttpContent content, int maximumBytes, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maximumBytes)
            throw new ProviderIntegrationException("Blaaiz returned an oversized response.");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > maximumBytes)
                throw new ProviderIntegrationException("Blaaiz returned an oversized response.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static ProviderIntegrationException InvalidToken() =>
        new("Blaaiz returned an invalid rates:read OAuth token response.");
    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
    public void Dispose() => _refresh.Dispose();
}
