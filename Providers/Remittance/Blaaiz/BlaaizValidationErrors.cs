using System.Text.Json;

namespace KorridorX.Providers.Remittance.Blaaiz;

public static class BlaaizValidationErrors
{
    // Only customer input responses are mapped to 422. Auth, outages and malformed
    // success responses retain the integration-error contract. Never forward raw bodies.
    public static IReadOnlyDictionary<string, string[]>? Read(string endpoint, int status, string body)
    {
        if (!(endpoint == "/api/external/customer" || endpoint.StartsWith("/api/external/customer/", StringComparison.Ordinal)) ||
            (status != 400 && status != 422)) return null;
        var result = new Dictionary<string, string[]>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    var messages = field.Value.ValueKind switch
                    {
                        JsonValueKind.String => new[] { field.Value.GetString()! },
                        JsonValueKind.Array => field.Value.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray(),
                        _ => Array.Empty<string>()
                    };
                    messages = messages.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
                    if (messages.Length > 0) result[field.Name] = messages;
                }
            }
        }
        catch (JsonException) { return null; }
        return status == 422 || result.Count > 0 ? result : null;
    }
}
