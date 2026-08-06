namespace KorridorX.Configuration;

public class BlaaizOptions
{
    public bool IsEnabled { get; set; }
    public string BaseUrl { get; set; } = "https://api-dev.blaaiz.com";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TokenEndpoint { get; set; } = "/oauth/token";
    public string Scopes { get; set; } = "customer:read customer:write collection:create collection:interac:create transaction:read wallet:read";
    public int TimeoutSeconds { get; set; } = 30;
    public int TokenRefreshBufferSeconds { get; set; } = 60;
    public Dictionary<string, string> CollectionWalletIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
