using System.Reflection;
using System.Text.Json;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizBankContractCompatibilityTests
{
    [Fact]
    public void Bank_contract_accepts_documented_string_identifiers()
    {
        const string json = """
        [
          {
            "id": "bank_123",
            "name": "Example Bank",
            "code": "123456",
            "country_id": "country_ng",
            "national_bank_code": "987654",
            "country": {
              "code": "NG",
              "name": "Nigeria"
            }
          }
        ]
        """;

        var bank = DeserializeSingleBank(json);

        Assert.Equal("bank_123", bank.Id);
        Assert.Equal("123456", bank.Code);
        Assert.Equal("country_ng", bank.CountryId);
        Assert.Equal("987654", bank.NationalBankCode);
        Assert.Equal("NG", bank.Country?.Code);
        Assert.Equal("Nigeria", bank.Country?.Name);
    }

    [Fact]
    public void Bank_contract_normalizes_numeric_provider_identifiers_to_strings()
    {
        const string json = """
        [
          {
            "id": 123,
            "name": "Example Bank",
            "code": 456789,
            "country_id": 99,
            "national_bank_code": 987654,
            "country": {
              "code": "NG",
              "name": "Nigeria"
            }
          }
        ]
        """;

        var bank = DeserializeSingleBank(json);

        Assert.Equal("123", bank.Id);
        Assert.Equal("456789", bank.Code);
        Assert.Equal("99", bank.CountryId);
        Assert.Equal("987654", bank.NationalBankCode);
        Assert.Equal("NG", bank.Country?.Code);
    }

    [Fact]
    public void Bank_contract_still_rejects_object_identifiers()
    {
        const string json = """
        [
          {
            "id": { "unexpected": true },
            "name": "Example Bank",
            "code": "123456",
            "country": {
              "code": "NG",
              "name": "Nigeria"
            }
          }
        ]
        """;

        Assert.Throws<JsonException>(() => DeserializeSingleBank(json));
    }

    private static BlaaizBankData DeserializeSingleBank(string json)
    {
        var serializerOptionsField = typeof(BlaaizApiClient).GetField(
            "SerializerOptions",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(serializerOptionsField);

        var serializerOptions = Assert.IsType<JsonSerializerOptions>(
            serializerOptionsField!.GetValue(null));

        var banks = JsonSerializer.Deserialize<List<BlaaizBankData>>(
            json,
            serializerOptions);

        Assert.NotNull(banks);
        return Assert.Single(banks!);
    }
}