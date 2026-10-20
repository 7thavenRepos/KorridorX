using System.Security.Cryptography;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Exceptions;

namespace KorridorX.Providers.Fx;

// Capture one immutable effective connection. Never expose credentials in a DTO, cache key or exception.
internal sealed class BlaaizFxConnection
{
    public const string Scope = "rates:read";
    public string Key { get; }
    public bool IsAvailable { get; }
    public Uri? ApiBaseUri { get; }
    public Uri? TokenUri { get; }
    public string ClientId { get; }
    public string ClientSecret { get; }
    public int RefreshBufferSeconds { get; }
    public int TimeoutSeconds { get; }

    public BlaaizFxConnection(BlaaizOptions options, string environmentName)
    {
        ClientId = options.ClientId?.Trim() ?? "";
        ClientSecret = options.ClientSecret ?? "";
        RefreshBufferSeconds = Math.Clamp(options.TokenRefreshBufferSeconds, 0, 300);
        TimeoutSeconds = Math.Clamp(options.TimeoutSeconds, 1, 60);
        var baseText = options.BaseUrl?.Trim() ?? "";
        var tokenText = options.TokenEndpoint?.Trim() ?? "";
        if (Uri.TryCreate(baseText.TrimEnd('/') + "/", UriKind.Absolute, out var api) && IsSafeHttps(api))
        {
            ApiBaseUri = api;
            if (!string.IsNullOrWhiteSpace(tokenText) && Uri.TryCreate(api, tokenText, out var token) &&
                IsSafeHttps(token) && string.Equals(api.Authority, token.Authority, StringComparison.OrdinalIgnoreCase))
                TokenUri = token;
        }

        var secretFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ClientSecret)));
        Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
            "KorridorX.Blaaiz.Fx.v1", environmentName?.Trim().ToUpperInvariant() ?? "",
            ApiBaseUri?.AbsoluteUri.TrimEnd('/') ?? baseText.TrimEnd('/'),
            TokenUri?.AbsoluteUri ?? tokenText, ClientId, secretFingerprint, Scope))));
        IsAvailable = options.IsEnabled && ApiBaseUri is not null && TokenUri is not null &&
            ClientId.Length > 0 && !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(environmentName);
    }

    public void EnsureAvailable()
    {
        if (!IsAvailable)
            throw new ProviderIntegrationException(
                "Blaaiz live FX reads require an enabled connection, credentials and valid HTTPS API/token endpoints on the same provider host.");
    }

    private static bool IsSafeHttps(Uri uri) => uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
