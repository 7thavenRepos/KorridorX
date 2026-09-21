using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizWalletData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("business_id")]
    public string BusinessId { get; set; } = "";
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";
    [JsonPropertyName("currency_id")]
    [JsonConverter(typeof(BlaaizCurrencyIdJsonConverter))]
    public string CurrencyId { get; set; } = "";
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

// The provider can return numeric currency IDs although its documented model uses strings.
// Keep this compatibility rule on currency_id, not on unrelated strings or money values.
public sealed class BlaaizCurrencyIdJsonConverter : JsonConverter<string>
{
    public override bool HandleNull => true;

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString()!;

        if (reader.TokenType == JsonTokenType.Number)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var value = document.RootElement.GetRawText();
            if (value.All(character => character is >= '0' and <= '9'))
                return value;
        }

        throw new JsonException("Blaaiz currency_id must be a string or a non-negative integer.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

public sealed class BlaaizSwapRequest
{
    [JsonPropertyName("from_business_wallet_id")]
    public string FromBusinessWalletId { get; set; } = "";

    [JsonPropertyName("to_business_wallet_id")]
    public string ToBusinessWalletId { get; set; } = "";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("amount_type")]
    public string AmountType { get; set; } = "from";
}

public sealed class BlaaizSwapResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("business_swap_transaction")]
    public BlaaizSwapTransactionData BusinessSwapTransaction { get; set; } = new();
}

public sealed class BlaaizSwapTransactionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("business_id")]
    public string BusinessId { get; set; } = "";

    [JsonPropertyName("business_user_id")]
    public string? BusinessUserId { get; set; }

    [JsonPropertyName("business_transaction_id")]
    public string BusinessTransactionId { get; set; } = "";

    [JsonPropertyName("from_business_wallet_id")]
    public string FromBusinessWalletId { get; set; } = "";

    [JsonPropertyName("from_currency")]
    public string FromCurrency { get; set; } = "";

    [JsonPropertyName("from_amount")]
    public decimal FromAmount { get; set; }

    [JsonPropertyName("from_amount_minus_fees")]
    public decimal FromAmountMinusFees { get; set; }

    [JsonPropertyName("to_currency")]
    public string ToCurrency { get; set; } = "";

    [JsonPropertyName("to_amount")]
    public decimal ToAmount { get; set; }

    [JsonPropertyName("from_exchange_rate")]
    public decimal FromExchangeRate { get; set; }

    [JsonPropertyName("custom_exchange_rate")]
    public decimal? CustomExchangeRate { get; set; }

    [JsonPropertyName("to_business_wallet_id")]
    public string ToBusinessWalletId { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("amount_type")]
    public string AmountType { get; set; } = "from";

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
