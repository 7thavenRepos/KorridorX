using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizCardCollectionRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "card";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("wallet_id")]
    public string WalletId { get; set; } = "";

    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("card_holder_name")]
    public string CardHolderName { get; set; } = "";

    [JsonPropertyName("card_number")]
    public string CardNumber { get; set; } = "";

    [JsonPropertyName("expiry")]
    public string Expiry { get; set; } = "";

    [JsonPropertyName("cvc")]
    public string Cvc { get; set; } = "";

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; set; }

    [JsonPropertyName("redirect_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedirectUrl { get; set; }
}

public sealed class BlaaizCardCollectionResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class BlaaizInteracMoneyRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("customer_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customer_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerId { get; set; }

    [JsonPropertyName("expiry_hours")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiryHours { get; set; }
}

public sealed class BlaaizInteracMoneyResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}
