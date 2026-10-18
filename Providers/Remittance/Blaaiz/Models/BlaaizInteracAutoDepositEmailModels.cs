using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

// https://docs.blaaiz.com/docs/payments/interac-auto-deposit-email/get-an-interac-auto-deposit-email
// Provider registration belongs to the authenticated business. The API does not return a wallet ID.
public sealed class BlaaizInteracAutoDepositEmailEnvelope
{
    [JsonPropertyName("data")]
    public BlaaizInteracAutoDepositEmailData? Data { get; set; }
}

public sealed class BlaaizInteracAutoDepositEmailData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    // PENDING and PROCESSING are not ready to receive money. Only ACTIVE is usable.
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class BlaaizInteracAutoDepositEmailListEnvelope
{
    [JsonPropertyName("data")]
    public List<BlaaizInteracAutoDepositEmailData> Data { get; set; } = [];

    [JsonPropertyName("links")]
    public BlaaizInteracAutoDepositEmailPageLinks? Links { get; set; }

    [JsonPropertyName("meta")]
    public BlaaizInteracAutoDepositEmailPageMeta? Meta { get; set; }
}

public sealed class BlaaizInteracAutoDepositEmailPageLinks
{
    [JsonPropertyName("first")]
    public string? First { get; set; }

    [JsonPropertyName("last")]
    public string? Last { get; set; }

    [JsonPropertyName("prev")]
    public string? Previous { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

public sealed class BlaaizInteracAutoDepositEmailPageMeta
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
