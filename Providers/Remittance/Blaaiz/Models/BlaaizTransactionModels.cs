using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizTransactionEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizTransactionData Data { get; set; } = new();
}

public sealed class BlaaizTransactionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("amount_without_fee")]
    public decimal? AmountWithoutFee { get; set; }

    [JsonPropertyName("fee")]
    public decimal? Fee { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("recipient")]
    public BlaaizTransactionRecipient? Recipient { get; set; }

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("question")]
    public string? Question { get; set; }

    [JsonPropertyName("answer")]
    public string? Answer { get; set; }
}

public sealed class BlaaizTransactionRecipient
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }

    [JsonPropertyName("bank_code")]
    public string? BankCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("routing_number")]
    public string? RoutingNumber { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
