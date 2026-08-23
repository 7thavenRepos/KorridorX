using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizCryptoWalletListResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public List<BlaaizCryptoWalletData> Data { get; set; } = [];
}

public sealed class BlaaizCryptoWalletData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("asset")]
    public BlaaizCryptoWalletAssetData Asset { get; set; } = new();

    [JsonPropertyName("balance")]
    public string Balance { get; set; } = "0";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class BlaaizCryptoWalletAssetData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("decimals")]
    public int Decimals { get; set; }
}

public sealed class BlaaizCryptoPayoutRequest
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("wallet_id")]
    public string WalletId { get; set; } = "";

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("network")]
    public string Network { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";
}

public sealed class BlaaizCryptoPayoutResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizCryptoPayoutData Data { get; set; } = new();
}

public sealed class BlaaizCryptoPayoutData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("wallet_id")]
    public string WalletId { get; set; } = "";

    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("destination_currency")]
    public string DestinationCurrency { get; set; } = "";

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "";

    [JsonPropertyName("fee")]
    public string? Fee { get; set; }

    [JsonPropertyName("net_amount")]
    public string? NetAmount { get; set; }

    [JsonPropertyName("destination_amount")]
    public string? DestinationAmount { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("network")]
    public string Network { get; set; } = "";
}

public sealed class BlaaizCryptoCollectionRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("wallet_id")]
    public string WalletId { get; set; } = "";

    [JsonPropertyName("network")]
    public string Network { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("customer_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerId { get; set; }
}

public sealed class BlaaizCryptoCollectionResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("transaction")]
    public BlaaizCryptoCollectionTransaction Transaction { get; set; } = new();
}

public sealed class BlaaizCryptoCollectionTransaction
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("token_amount")]
    public decimal TokenAmount { get; set; }

    [JsonPropertyName("network")]
    public string Network { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}
