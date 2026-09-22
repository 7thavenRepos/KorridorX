using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizOwnerUploadUrlRequest
{
    [JsonPropertyName("file_category")]
    public string FileCategory { get; set; } = "id_document_front";
}

[JsonConverter(typeof(BlaaizUploadUrlEnvelopeConverter))]
public sealed class BlaaizUploadUrlEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizUploadUrlData Data { get; set; } = new();

}

public sealed class BlaaizUploadUrlData
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BlaaizOwnerFilesRequest
{
    [JsonPropertyName("id_document_front")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdDocumentFront { get; set; }

    [JsonPropertyName("id_document_back")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdDocumentBack { get; set; }
}

public sealed class BlaaizBusinessDocumentRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class BlaaizBusinessDocumentEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizBusinessDocumentData Data { get; set; } = new();
}

public sealed class BlaaizBusinessOwnerEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizBusinessOwnerData Data { get; set; } = new();
}
