using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

/// <summary>Normalizes Blaaiz's live root response and its documented data envelope.</summary>
public sealed class BlaaizUploadUrlEnvelopeConverter : JsonConverter<BlaaizUploadUrlEnvelope>
{
    public override BlaaizUploadUrlEnvelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Blaaiz upload response must be an object.");

        var data = root;
        if (root.TryGetProperty("data", out var nested))
        {
            // Never combine partial root values with a different nested target.
            if (nested.ValueKind != JsonValueKind.Object || root.TryGetProperty("file_id", out _) ||
                root.TryGetProperty("url", out _) || root.TryGetProperty("headers", out _))
                throw new JsonException("Blaaiz upload response has an invalid or ambiguous envelope.");
            data = nested;
        }

        var fileId = RequiredString(data, "file_id");
        var url = RequiredString(data, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
            throw new JsonException("Blaaiz upload URL must be an absolute HTTPS URL without user credentials.");

        if (!data.TryGetProperty("headers", out var headerObject) || headerObject.ValueKind != JsonValueKind.Object)
            throw new JsonException("Blaaiz upload headers must be an object.");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in headerObject.EnumerateObject())
        {
            if (!seen.Add(property.Name) || string.IsNullOrWhiteSpace(property.Name))
                throw new JsonException("Blaaiz upload headers contain a duplicate or empty name.");
            var values = property.Value.ValueKind switch
            {
                JsonValueKind.String => new[] { HeaderValue(property.Value) },
                JsonValueKind.Array => property.Value.EnumerateArray().Select(HeaderValue).ToArray(),
                _ => throw new JsonException("Blaaiz upload header values must be strings or arrays of strings.")
            };
            if (values.Length == 0)
                throw new JsonException("Blaaiz upload header arrays must not be empty.");

            if (property.Name.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                if (values.Length != 1 || !values[0].Equals(uri.Authority, StringComparison.OrdinalIgnoreCase))
                    throw new JsonException("Blaaiz upload Host header does not match its URL.");
                // Host is browser-controlled and is generated from the URL.
                continue;
            }
            headers.Add(property.Name, string.Join(",", values));
        }

        return new BlaaizUploadUrlEnvelope
        {
            Message = root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                ? message.GetString()! : "",
            Data = new BlaaizUploadUrlData { FileId = fileId, Url = url, Headers = headers }
        };
    }

    private static string RequiredString(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new JsonException($"Blaaiz upload response is missing a valid {name}.");
        return value.GetString()!;
    }

    private static string HeaderValue(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new JsonException("Blaaiz upload header value must be a non-empty string.");
        var text = value.GetString()!;
        if (text.Contains('\r') || text.Contains('\n'))
            throw new JsonException("Blaaiz upload header value must not contain a newline.");
        return text;
    }

    public override void Write(Utf8JsonWriter writer, BlaaizUploadUrlEnvelope value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("message", value.Message);
        writer.WritePropertyName("data");
        JsonSerializer.Serialize(writer, value.Data, options);
        writer.WriteEndObject();
    }
}
