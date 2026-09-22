using System.Text.Json.Serialization;

namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed class BlaaizCreateCustomerRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "individual";

    [JsonPropertyName("first_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; set; }

    [JsonPropertyName("business_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BusinessName { get; set; }

    [JsonPropertyName("trading_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TradingName { get; set; }

    [JsonPropertyName("business_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BusinessType { get; set; }

    [JsonPropertyName("registration_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("incorporation_country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IncorporationCountry { get; set; }

    [JsonPropertyName("incorporation_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IncorporationDate { get; set; }

    [JsonPropertyName("industry_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IndustryType { get; set; }

    [JsonPropertyName("business_description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BusinessDescription { get; set; }

    [JsonPropertyName("website")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Website { get; set; }

    [JsonPropertyName("source_of_funds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceOfFunds { get; set; }

    [JsonPropertyName("estimated_annual_revenue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EstimatedAnnualRevenue { get; set; }

    [JsonPropertyName("expected_monthly_payments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpectedMonthlyPayments { get; set; }

    [JsonPropertyName("account_purpose")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountPurpose { get; set; }

    [JsonPropertyName("kyb_scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KybScope { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("id_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdType { get; set; }

    [JsonPropertyName("id_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdNumber { get; set; }

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; set; }

    [JsonPropertyName("tin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tin { get; set; }

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

    [JsonPropertyName("operating_street")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingStreet { get; set; }

    [JsonPropertyName("operating_city")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingCity { get; set; }

    [JsonPropertyName("operating_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingState { get; set; }

    [JsonPropertyName("operating_zip_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingZipCode { get; set; }

    [JsonPropertyName("operating_country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingCountry { get; set; }

    [JsonPropertyName("id_expiry_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdExpiryDate { get; set; }

    [JsonPropertyName("id_issue_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdIssueDate { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BlaaizBusinessOwnerRequest>? Owners { get; set; }
}

public sealed class BlaaizBusinessOwnerRequest
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("date_of_birth")]
    public string DateOfBirth { get; set; } = "";

    [JsonPropertyName("nationality")]
    public string Nationality { get; set; } = "";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("ownership_percentage")]
    public decimal OwnershipPercentage { get; set; }

    [JsonPropertyName("has_control")]
    public bool HasControl { get; set; }

    [JsonPropertyName("is_signer")]
    public bool IsSigner { get; set; }

    [JsonPropertyName("is_beneficial_owner")]
    public bool IsBeneficialOwner { get; set; }

    [JsonPropertyName("id_document_type")]
    public string IdDocumentType { get; set; } = "";

    [JsonPropertyName("id_document_number")]
    public string IdDocumentNumber { get; set; } = "";

    [JsonPropertyName("id_document_country")]
    public string IdDocumentCountry { get; set; } = "";

    [JsonPropertyName("id_expiry_date")]
    public string IdExpiryDate { get; set; } = "";

    [JsonPropertyName("is_pep")]
    public bool IsPep { get; set; }
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

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("business_name")]
    public string? BusinessName { get; set; }

    [JsonPropertyName("verification_status")]
    public string VerificationStatus { get; set; } = "PENDING";

    [JsonPropertyName("kyb_scope")]
    public string? KybScope { get; set; }

    [JsonPropertyName("owners")]
    public List<BlaaizBusinessOwnerData> Owners { get; set; } = [];

    [JsonPropertyName("documents")]
    public List<BlaaizBusinessDocumentData> Documents { get; set; } = [];
}

public sealed class BlaaizBusinessOwnerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = "";

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("ownership_percentage")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal OwnershipPercentage { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PENDING";

    [JsonPropertyName("admin_comments")]
    public object? AdminComments { get; set; }
}

public sealed class BlaaizBusinessDocumentData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PENDING";

    [JsonPropertyName("admin_comments")]
    public object? AdminComments { get; set; }
}
