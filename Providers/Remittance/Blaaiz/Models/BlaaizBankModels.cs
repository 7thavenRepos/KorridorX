using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizBankData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("country_id")]
    public string? CountryId { get; set; }

    [JsonPropertyName("national_bank_code")]
    public string? NationalBankCode { get; set; }

    [JsonPropertyName("country")]
    public BlaaizBankCountryData? Country { get; set; }
}

public sealed class BlaaizBankCountryData
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class BlaaizResolveBankAccountRequest
{
    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = "";

    [JsonPropertyName("bank_id")]
    public string BankId { get; set; } = "";
}

public sealed class BlaaizResolveBankAccountResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = "";
}
