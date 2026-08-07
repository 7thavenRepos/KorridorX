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
    public string CurrencyId { get; set; } = "";
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}
