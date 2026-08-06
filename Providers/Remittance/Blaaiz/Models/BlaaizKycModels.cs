using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizKycUploadUrlRequest
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("file_category")]
    public string FileCategory { get; set; } = "";
}

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
