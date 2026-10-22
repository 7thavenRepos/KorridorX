using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

// Blaaiz's fee endpoint returns a list for each payout rail. Do not mistake the
// top-level "our_fee" or a collection-method quote for a payout-method fee.
public sealed class BlaaizFeeBreakdownRequest
{
    [JsonPropertyName("from_currency_id")]
    public string FromCurrencyId { get; init; } = "";

    [JsonPropertyName("to_currency_id")]
    public string ToCurrencyId { get; init; } = "";

    [JsonPropertyName("from_amount")]
    public decimal FromAmount { get; init; }

    [JsonPropertyName("customer_id")]
    public string CustomerId { get; init; } = "";
}

public sealed class BlaaizFeeBreakdownResponse
{
    [JsonPropertyName("payout_fees")]
    public List<BlaaizPayoutFeeBreakdown>? PayoutFees { get; set; }
}

public sealed class BlaaizPayoutFeeBreakdown
{
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("fee")]
    public decimal? MethodFee { get; set; }

    [JsonPropertyName("our_fee")]
    public decimal? BaseFee { get; set; }

    [JsonPropertyName("total_fees")]
    public decimal? TotalFees { get; set; }

    [JsonPropertyName("you_send")]
    public decimal? YouSend { get; set; }

    [JsonPropertyName("total_amount_to_send")]
    public decimal? NetAmountToSend { get; set; }

    [JsonPropertyName("recipient_gets")]
    public decimal? RecipientGets { get; set; }

    [JsonPropertyName("rate")]
    public decimal? Rate { get; set; }

    [JsonPropertyName("amountIsYouSend")]
    public bool? AmountIsYouSend { get; set; }

    [JsonPropertyName("amountIsRecipientGets")]
    public bool? AmountIsRecipientGets { get; set; }
}
