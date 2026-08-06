using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizWebhookReplayRequest
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";
}

public sealed class BlaaizWebhookReplayResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class BlaaizRefundRequest
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = "";
}

public sealed class BlaaizRefundEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizRefundData Data { get; set; } = new();
}

public sealed class BlaaizRefundData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("refund_reference")]
    public string? RefundReference { get; set; }

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
