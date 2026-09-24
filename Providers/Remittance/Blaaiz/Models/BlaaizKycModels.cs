using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizKycUploadUrlRequest
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("file_category")]
    public string FileCategory { get; set; } = "";
}

[JsonConverter(typeof(BlaaizKycUploadUrlResponseJsonConverter))]
public sealed class BlaaizKycUploadUrlResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class BlaaizKycUploadUrlResponseJsonConverter
    : JsonConverter<BlaaizKycUploadUrlResponse>
{
    public override BlaaizKycUploadUrlResponse? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("KYC upload response must be a JSON object.");
        }

        var target = root;
        if (root.TryGetProperty("data", out var dataElement))
        {
            if (dataElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("KYC upload response data must be a JSON object.");
            }

            target = dataElement;
        }

        var fileId = RequiredString(target, "file_id");
        var url = RequiredString(target, "url");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uploadUri) ||
            uploadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new JsonException("KYC upload URL must be an absolute HTTPS URL.");
        }

        var headers = ReadHeaders(target, uploadUri);

        var message = "";
        if (root.TryGetProperty("message", out var rootMessage) &&
            rootMessage.ValueKind == JsonValueKind.String)
        {
            message = rootMessage.GetString() ?? "";
        }
        else if (target.TryGetProperty("message", out var targetMessage) &&
                 targetMessage.ValueKind == JsonValueKind.String)
        {
            message = targetMessage.GetString() ?? "";
        }

        return new BlaaizKycUploadUrlResponse
        {
            Message = message,
            FileId = fileId,
            Url = url,
            Headers = headers
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        BlaaizKycUploadUrlResponse value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("message", value.Message);
        writer.WriteString("file_id", value.FileId);
        writer.WriteString("url", value.Url);
        writer.WritePropertyName("headers");
        JsonSerializer.Serialize(writer, value.Headers, options);
        writer.WriteEndObject();
    }

    private static Dictionary<string, string> ReadHeaders(
        JsonElement target,
        Uri uploadUri)
    {
        if (!target.TryGetProperty("headers", out var headersElement) ||
            headersElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("KYC upload response headers must be a JSON object.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in headersElement.EnumerateObject())
        {
            var name = property.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new JsonException("KYC upload response contains an empty header name.");
            }

            var values = ReadHeaderValues(property.Value);
            if (values.Count == 0)
            {
                throw new JsonException($"KYC upload header '{name}' has no values.");
            }

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) ||
                    value.Contains('\r') ||
                    value.Contains('\n'))
                {
                    throw new JsonException($"KYC upload header '{name}' contains an invalid value.");
                }
            }

            if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                if (values.Count != 1 ||
                    !string.Equals(
                        values[0].Trim(),
                        uploadUri.Host,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new JsonException("KYC upload Host header does not match the signed upload URL.");
                }

                // Host is controlled by the HTTP client from the signed URL.
                // Never ask mobile/web clients to set it manually.
                continue;
            }

            if (headers.ContainsKey(name))
            {
                throw new JsonException($"KYC upload response contains duplicate header '{name}'.");
            }

            headers[name] = string.Join(",", values);
        }

        return headers;
    }

    private static List<string> ReadHeaderValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new List<string> { element.GetString() ?? "" };
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("KYC upload header values must be strings or arrays of strings.");
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("KYC upload header arrays must contain only strings.");
            }

            values.Add(item.GetString() ?? "");
        }

        return values;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"KYC upload response requires '{propertyName}'.");
        }

        return property.GetString()!;
    }
}

public sealed class BlaaizAttachCustomerFilesRequest
{
    [JsonPropertyName("id_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentityFileId { get; set; }

    [JsonPropertyName("id_file_back")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentityBackFileId { get; set; }

    [JsonPropertyName("proof_of_address_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProofOfAddressFileId { get; set; }

    [JsonPropertyName("liveness_check_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LivenessCheckFileId { get; set; }
}

public sealed class BlaaizMessageResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
