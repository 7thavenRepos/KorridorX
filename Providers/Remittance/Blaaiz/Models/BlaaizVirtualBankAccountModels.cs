using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizVirtualBankAccountRequest
{
    [JsonPropertyName("wallet_id")]
    public string WalletId { get; init; } = "";

    [JsonPropertyName("customer_id")]
    public string CustomerId { get; init; } = "";
}

public sealed class BlaaizVirtualBankAccountEnvelope
{
    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }
}
