namespace KorridorX.Configuration;

public class BlaaizOptions
{
    public bool IsEnabled { get; set; }
    public string BaseUrl { get; set; } = "https://api-dev.blaaiz.com";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TokenEndpoint { get; set; } = "/oauth/token";
    public string Scopes { get; set; } = "customer:read customer:write file:upload collection:create collection:interac:create collection:crypto:create payout:create transaction:read wallet:read bank:read crypto-wallet:read crypto-payout:create crypto-transaction:read";
    public int TimeoutSeconds { get; set; } = 30;
    public int TokenRefreshBufferSeconds { get; set; } = 60;
    public string WebhookSigningSecret { get; set; } = "";
    public int WebhookTimestampToleranceMinutes { get; set; } = 5;
    // Legacy import only. Runtime wallet selection is managed through SuperAdmin/database registrations.
    public Dictionary<string, string> CollectionWalletIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PayoutWalletIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> CryptoWalletIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> CryptoNetworkMappings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ETHEREUM"] = "ETHEREUM_MAINNET",
            ["POLYGON"] = "MATIC_MAINNET",
            ["TRON"] = "TRON_MAINNET",
            ["BSC"] = "BSC_MAINNET"
        };

    public bool ReconciliationEnabled { get; set; } = true;
    public int ReconciliationIntervalMinutes { get; set; } = 5;
    public int ReconciliationBatchSize { get; set; } = 50;
    public bool AutomaticPayoutDispatchEnabled { get; set; } = false;
    public int PayoutDispatchIntervalSeconds { get; set; } = 30;
    public bool MinimalBusinessKybEnabled { get; set; } = false;
}
