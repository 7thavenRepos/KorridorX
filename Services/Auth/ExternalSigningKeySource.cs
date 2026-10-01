using Microsoft.IdentityModel.Tokens;

namespace KorridorX.Services.Auth;

public interface IExternalSigningKeySource
{
    Task<IReadOnlyList<SecurityKey>> GetAsync(string provider, string kid, CancellationToken ct);
}

public sealed class ExternalIdentityProviderUnavailableException : Exception
{
    public ExternalIdentityProviderUnavailableException() : base("The sign-in provider is temporarily unavailable. Try again shortly.") { }
}

// Only these pinned public URLs are fetched. JWT jku/x5u and client input never select a URL.
public sealed class ExternalSigningKeySource(IHttpClientFactory http) : IExternalSigningKeySource, IDisposable
{
    public const string HttpClientName = "ConsumerExternalIdentityKeys";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, KeyCache> _cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<SecurityKey>> GetAsync(string provider, string kid, CancellationToken ct)
    {
        provider = ExternalIdentityPolicy.Provider(provider);
        await _gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_cache.TryGetValue(provider, out var cached))
            {
                if (cached.ExpiresAt > now && cached.Keys.Any(x => x.KeyId == kid)) return cached.Keys;
                // Throttle unknown-kid refreshes, including fetch failures, across requests.
                if (cached.LastAttempt > now.AddSeconds(-30)) throw ExternalIdentityPolicy.InvalidToken();
            }
            _cache[provider] = new(cached?.Keys ?? [], now, cached?.ExpiresAt ?? now);
            try
            {
                var client = http.CreateClient(HttpClientName);
                var url = provider == "google" ? "https://www.googleapis.com/oauth2/v3/certs" : "https://appleid.apple.com/auth/keys";
                using var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync(ct);
                if (text.Length > 65536) throw new ExternalIdentityProviderUnavailableException();
                var jwks = new JsonWebKeySet(text);
                var allowed = jwks.Keys.Where(x => x.Kty == "RSA" && (string.IsNullOrEmpty(x.Use) || x.Use == "sig") &&
                    (string.IsNullOrEmpty(x.Alg) || x.Alg == SecurityAlgorithms.RsaSha256) && !string.IsNullOrEmpty(x.Kid)).Cast<SecurityKey>().ToArray();
                if (allowed.Length is 0 or > 100 || allowed.Select(x => x.KeyId).Distinct(StringComparer.Ordinal).Count() != allowed.Length)
                    throw new ExternalIdentityProviderUnavailableException();
                var ttl = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromMinutes(15);
                ttl = TimeSpan.FromSeconds(Math.Clamp(ttl.TotalSeconds, 30, 900));
                _cache[provider] = new(allowed, now, now.Add(ttl));
                if (!allowed.Any(x => x.KeyId == kid)) throw ExternalIdentityPolicy.InvalidToken();
                return allowed;
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or ArgumentException)
            { throw new ExternalIdentityProviderUnavailableException(); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { throw new ExternalIdentityProviderUnavailableException(); }
        }
        finally { _gate.Release(); }
    }
    public void Dispose() => _gate.Dispose();
    private sealed record KeyCache(IReadOnlyList<SecurityKey> Keys, DateTimeOffset LastAttempt, DateTimeOffset ExpiresAt);
}
