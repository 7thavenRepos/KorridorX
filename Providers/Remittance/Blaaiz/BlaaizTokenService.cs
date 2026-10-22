using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Remittance.Blaaiz;

public class BlaaizTokenService : IBlaaizTokenService
{
    private const string CacheKey = "Blaaiz:OAuthAccessToken";
    private const string InteracReadScope = "interac-auto-deposit-email:read";
    private const string FeeReadScope = "fees:read";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly BlaaizOptions _options;
    private readonly IProviderRequestAuditService _auditService;

    public BlaaizTokenService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<BlaaizOptions> options,
        IProviderRequestAuditService auditService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
        _auditService = auditService;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        EnsureEnabled();

        if (_cache.TryGetValue<string>(CacheKey, out var cachedToken) &&
            !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<string>(CacheKey, out cachedToken) &&
                !string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            return await AcquireTokenAsync(ct);
        }
        finally
        {
            TokenLock.Release();
        }
    }

    public Task<string> GetInteracAutoDepositReadAccessTokenAsync(CancellationToken ct = default) =>
        GetScopedReadAccessTokenAsync(InteracReadScope, "InteracRead", "Interac registration reads", ct);

    public Task<string> GetFeeReadAccessTokenAsync(CancellationToken ct = default) =>
        GetScopedReadAccessTokenAsync(FeeReadScope, "FeeRead", "withdrawal fee previews", ct);

    private async Task<string> GetScopedReadAccessTokenAsync(string scope, string cacheCategory,
        string purpose, CancellationToken ct)
    {
        EnsureEnabled();
        var connection = CaptureInteracReadConnection(purpose);
        // Capture credentials/endpoints once so rotation cannot put a new connection's token under an old key.
        var identity = JsonSerializer.Serialize(new[]
        {
            connection.ApiBaseUri.AbsoluteUri,
            connection.TokenUri.AbsoluteUri,
            connection.ClientId,
            connection.ClientSecret,
            scope
        });
        var scopedKey = "Blaaiz:OAuth:" + cacheCategory + ":" +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

        if (_cache.TryGetValue<string>(scopedKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<string>(scopedKey, out cached) && !string.IsNullOrWhiteSpace(cached))
                return cached;

            return await AcquireInteracReadTokenAsync(connection, scopedKey, scope, purpose, ct);
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private InteracReadConnection CaptureInteracReadConnection(string purpose)
    {
        if (!Uri.TryCreate((_options.BaseUrl ?? "").TrimEnd('/') + "/", UriKind.Absolute, out var apiBaseUri) ||
            apiBaseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(apiBaseUri.UserInfo) ||
            !string.IsNullOrEmpty(apiBaseUri.Fragment) ||
            string.IsNullOrWhiteSpace(_options.TokenEndpoint) ||
            !Uri.TryCreate(apiBaseUri, _options.TokenEndpoint, out var tokenUri) ||
            tokenUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(apiBaseUri.Authority, tokenUri.Authority, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(tokenUri.UserInfo) ||
            !string.IsNullOrEmpty(tokenUri.Fragment))
        {
            throw new InvalidOperationException($"Blaaiz {purpose} require a token endpoint on the configured HTTPS provider connection.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException($"The current Blaaiz connection has no OAuth credentials for {purpose}.");

        return new InteracReadConnection(apiBaseUri, tokenUri, _options.ClientId, _options.ClientSecret);
    }

    private async Task<string> AcquireInteracReadTokenAsync(InteracReadConnection connection, string scopedKey,
        string scope, string purpose, CancellationToken ct)
    {
        var requestLogId = await _auditService.StartAsync(
            ProviderCode.Blaaiz,
            connection.TokenUri.AbsolutePath,
            HttpMethod.Post.Method,
            JsonSerializer.Serialize(new { contentType = "application/x-www-form-urlencoded" }),
            JsonSerializer.Serialize(new
            {
                grant_type = "client_credentials",
                client_id = Mask(connection.ClientId),
                client_secret = "***REDACTED***",
                scope
            }),
            ct: ct);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = connection.ClientId,
                ["client_secret"] = connection.ClientSecret,
                ["scope"] = scope
            });
            var client = _httpClientFactory.CreateClient("BlaaizAuth");
            using var response = await client.PostAsync(connection.TokenUri, body, ct);
            var rawResponse = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var message = $"Blaaiz could not authorize {purpose}. Confirm the current connection is authorized for {scope}. Existing credentials and configured scopes were not changed.";
                // OAuth errors can echo request secrets or token material; never retain their raw response.
                var safeResponse = JsonSerializer.Serialize(new { message });
                await _auditService.FailAsync(requestLogId, (int)response.StatusCode, safeResponse, message, stopwatch.ElapsedMilliseconds, ct);
                throw new ProviderIntegrationException(message, (int)response.StatusCode, safeResponse, requestLogId);
            }

            BlaaizTokenResponse? token = null;
            try { token = JsonSerializer.Deserialize<BlaaizTokenResponse>(rawResponse, JsonOptions()); }
            catch (JsonException) { }

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0 ||
                !string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                (token.Scope is not null && !string.Equals(token.Scope.Trim(), scope, StringComparison.Ordinal)))
            {
                var message = $"Blaaiz returned an invalid OAuth response for the {scope} scope.";
                var safeResponse = JsonSerializer.Serialize(new { message });
                await _auditService.FailAsync(requestLogId, (int)response.StatusCode, safeResponse, message, stopwatch.ElapsedMilliseconds, ct);
                throw new ProviderIntegrationException(message, (int)response.StatusCode, safeResponse, requestLogId);
            }

            await _auditService.CompleteAsync(requestLogId, (int)response.StatusCode,
                JsonSerializer.Serialize(new { token_type = token.TokenType, expires_in = token.ExpiresIn, access_token = "***REDACTED***", scope }),
                stopwatch.ElapsedMilliseconds, ct);

            var cacheSeconds = Math.Max(1, token.ExpiresIn - Math.Max(0, _options.TokenRefreshBufferSeconds));
            _cache.Set(scopedKey, token.AccessToken, TimeSpan.FromSeconds(cacheSeconds));
            return token.AccessToken;
        }
        catch (ProviderIntegrationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var message = $"Unable to connect to Blaaiz authentication for {purpose}.";
            await _auditService.FailAsync(requestLogId, null, null, message, stopwatch.ElapsedMilliseconds, ct);
            throw new ProviderIntegrationException(message, requestLogId: requestLogId, innerException: ex);
        }
    }

    private sealed record InteracReadConnection(Uri ApiBaseUri, Uri TokenUri, string ClientId, string ClientSecret)
    {
        public override string ToString() => "Blaaiz Interac registration read connection";
    }

    private async Task<string> AcquireTokenAsync(CancellationToken ct)
    {
        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = _options.Scopes
        };

        var auditBody = JsonSerializer.Serialize(new
        {
            grant_type = "client_credentials",
            client_id = Mask(_options.ClientId),
            client_secret = "***REDACTED***",
            scope = _options.Scopes
        });

        var requestLogId = await _auditService.StartAsync(
            ProviderCode.Blaaiz,
            _options.TokenEndpoint,
            HttpMethod.Post.Method,
            JsonSerializer.Serialize(new { contentType = "application/x-www-form-urlencoded" }),
            auditBody,
            ct: ct);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient("BlaaizAuth");
            using var response = await client.PostAsync(
                _options.TokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                ct);

            var rawResponse = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(rawResponse, "Unable to authenticate with Blaaiz.");

                await _auditService.FailAsync(
                    requestLogId,
                    (int)response.StatusCode,
                    rawResponse,
                    message,
                    stopwatch.ElapsedMilliseconds,
                    ct);

                throw new ProviderIntegrationException(
                    message,
                    (int)response.StatusCode,
                    rawResponse,
                    requestLogId);
            }

            var tokenResponse = JsonSerializer.Deserialize<BlaaizTokenResponse>(
                rawResponse,
                JsonOptions());

            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                const string message = "Blaaiz returned an invalid OAuth response.";

                await _auditService.FailAsync(
                    requestLogId,
                    (int)response.StatusCode,
                    rawResponse,
                    message,
                    stopwatch.ElapsedMilliseconds,
                    ct);

                throw new ProviderIntegrationException(
                    message,
                    (int)response.StatusCode,
                    rawResponse,
                    requestLogId);
            }

            await _auditService.CompleteAsync(
                requestLogId,
                (int)response.StatusCode,
                JsonSerializer.Serialize(new
                {
                    token_type = tokenResponse.TokenType,
                    expires_in = tokenResponse.ExpiresIn,
                    access_token = "***REDACTED***"
                }),
                stopwatch.ElapsedMilliseconds,
                ct);

            var cacheSeconds = Math.Max(
                30,
                tokenResponse.ExpiresIn - _options.TokenRefreshBufferSeconds);

            _cache.Set(
                CacheKey,
                tokenResponse.AccessToken,
                TimeSpan.FromSeconds(cacheSeconds));

            return tokenResponse.AccessToken;
        }
        catch (ProviderIntegrationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _auditService.FailAsync(
                requestLogId,
                null,
                null,
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                ct);

            throw new ProviderIntegrationException(
                "Unable to connect to Blaaiz authentication.",
                requestLogId: requestLogId,
                innerException: ex);
        }
    }

    private void EnsureEnabled()
    {
        if (!_options.IsEnabled)
        {
            throw new InvalidOperationException(
                "Blaaiz integration is disabled. Set Blaaiz:IsEnabled to true after configuring valid credentials.");
        }
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 4)
        {
            return "****";
        }

        return $"{value[..2]}***{value[^2..]}";
    }

    private static string ExtractErrorMessage(string rawResponse, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;

            if (root.TryGetProperty("error_description", out var description))
            {
                return description.GetString() ?? fallback;
            }

            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };
}
