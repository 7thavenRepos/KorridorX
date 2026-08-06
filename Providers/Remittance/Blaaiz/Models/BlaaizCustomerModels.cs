using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizCreateCustomerRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "individual";

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("id_type")]
    public string IdType { get; set; } = "";

    [JsonPropertyName("id_number")]
    public string IdNumber { get; set; } = "";

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; set; }

    [JsonPropertyName("dob")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("street")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Street { get; set; }

    [JsonPropertyName("city")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    [JsonPropertyName("zip_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZipCode { get; set; }

    [JsonPropertyName("id_expiry_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdExpiryDate { get; set; }

    [JsonPropertyName("id_issue_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdIssueDate { get; set; }
}

public sealed class BlaaizCustomerEnvelope
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public BlaaizCustomerData Data { get; set; } = new();
}

public sealed class BlaaizCustomerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("verification_status")]
    public string VerificationStatus { get; set; } = "PENDING";
}
