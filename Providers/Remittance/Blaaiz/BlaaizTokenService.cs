using System.Diagnostics;
using System.Net.Http.Json;
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
