using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using KorridorX.Exceptions;

namespace KorridorX.Providers.Fx;

public sealed record BlaaizSelectedFxRate(
    string SourceCurrencyCode, string DestinationCurrencyCode, decimal Rate,
    string ProviderRateId, DateTime? ProviderTimestampUtc, bool SendEnabled, bool SwapEnabled,
    string? Category, string? RateType);

public static class BlaaizFxRateParser
{
    // Official shape: https://docs.business.blaaiz.com/api-reference/rate/list-rates
    // search_term is a partial filter; the returned pair must still match exactly and directionally.
    public static BlaaizSelectedFxRate Parse(
        string json, string sourceCurrencyCode, string destinationCurrencyCode, string? providerRateId = null)
    {
        var source = NormalizeCurrency(sourceCurrencyCode);
        var destination = NormalizeCurrency(destinationCurrencyCode);
        if (source == destination) throw new InvalidOperationException("FX source and destination currencies must differ.");
        var selectedId = NormalizeOptionalRateId(providerRateId);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1) ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                throw InvalidResponse("Blaaiz returned no rate collection.");
            var candidates = new List<(JsonElement Row, string Id, bool Send, bool Swap)>();
            foreach (var row in data.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) throw InvalidResponse("Blaaiz returned a malformed rate row.");
                var pair = Text(row, "pair");
                if (!string.Equals(pair?.Trim(), source + "/" + destination, StringComparison.OrdinalIgnoreCase)) continue;
                if (row.EnumerateObject().GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1))
                    throw InvalidResponse("Blaaiz returned duplicate fields in a matching rate.");
                var id = Identifier(row);
                if (selectedId is not null && !string.Equals(id, selectedId, StringComparison.Ordinal)) continue;
                var send = Flag(row, "send_enabled");
                var swap = Flag(row, "swap_enabled");
                if (Flag(row, "is_active") && (send || swap)) candidates.Add((row, id, send, swap));
            }
            if (candidates.Count == 0)
                throw InvalidResponse("Blaaiz has no active, usable rate for the exact requested direction and selected rate ID.");
            if (candidates.Count != 1)
                throw InvalidResponse("Blaaiz returned multiple usable rates for this direction. Select an explicit provider rate ID.");

            var selected = candidates[0];
            if (!selected.Row.TryGetProperty("value", out var value) || !TryRate(value, out var rate) || rate <= 0)
                throw InvalidResponse("Blaaiz returned an invalid exchange-rate value.");
            return new BlaaizSelectedFxRate(source, destination, rate, selected.Id,
                ProviderTime(Text(selected.Row, "updated_at")), selected.Send, selected.Swap,
                Text(selected.Row, "category"), Text(selected.Row, "type"));
        }
        catch (JsonException)
        {
            throw InvalidResponse("Blaaiz returned incompatible FX rate JSON.");
        }
    }

    // The NGN -> major-fiat rates in Blaaiz's current NGN quote convention
    // are prices in NGN per one unit of the destination fiat currency.
    // Every KorridorX customer rate must instead be destination units per
    // one source unit, because quote and valuation consumers multiply.
    // Never infer a reciprocal from rate magnitude alone on other pairs.
    public static decimal DestinationUnitsPerSource(BlaaizSelectedFxRate selected)
    {
        if (selected.SourceCurrencyCode == "NGN" &&
            (selected.DestinationCurrencyCode is "CAD" or "EUR" or "GBP" or "USD"))
        {
            if (selected.Rate <= 1m)
                throw InvalidResponse("The Blaaiz NGN-to-foreign quote does not match the configured NGN-per-destination-unit convention.");
            var normalized = decimal.Round(1m / selected.Rate, 8, MidpointRounding.AwayFromZero);
            if (normalized <= 0m)
                throw InvalidResponse("The Blaaiz NGN-to-foreign quote cannot be represented as a positive conversion rate.");
            return normalized;
        }
        return selected.Rate;
    }

    public static string NormalizeCurrency(string value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(code, "^[A-Z0-9]{2,20}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("A valid FX asset code is required.");
        return code;
    }

    public static string? NormalizeOptionalRateId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var id = value.Trim();
        if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,150}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The provider rate ID is invalid.");
        return id;
    }

    private static string Identifier(JsonElement row)
    {
        if (!row.TryGetProperty("id", out var value) || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            throw InvalidResponse("Blaaiz returned a rate without an identifier.");
        var id = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        if (id is null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,150}$", RegexOptions.CultureInvariant))
            throw InvalidResponse("Blaaiz returned an invalid rate identifier.");
        return id;
    }

    private static string? Text(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw InvalidResponse("Blaaiz returned an incompatible rate field.");
        return value.GetString();
    }

    private static bool Flag(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw InvalidResponse("Blaaiz returned an incompatible rate capability.");
        return value.GetBoolean();
    }

    private static bool TryRate(JsonElement value, out decimal rate)
    {
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDecimal(out rate);
        if (value.ValueKind == JsonValueKind.String)
            return decimal.TryParse(value.GetString()?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out rate);
        rate = 0;
        return false;
    }

    private static DateTime? ProviderTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (!Regex.IsMatch(text, "(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            throw InvalidResponse("Blaaiz returned an invalid or unqualified provider rate timestamp.");
        return timestamp.UtcDateTime;
    }

    private static ProviderIntegrationException InvalidResponse(string message) => new(message);
}
