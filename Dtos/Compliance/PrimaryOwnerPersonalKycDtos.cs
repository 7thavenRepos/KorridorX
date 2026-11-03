using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

/// <summary>
/// The primary business account owner remains a Business user. These details
/// create a SEPARATE individual KYC profile under the same login, not a link
/// to any other KorridorX Consumer or Blaaiz customer.
/// </summary>
public sealed class PreparePrimaryOwnerPersonalKycRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = "";

    [Required]
    public DateTime? DateOfBirth { get; set; }

    [Required, StringLength(10, MinimumLength = 2)]
    public string CountryCode { get; set; } = "";

    [Required, StringLength(250, MinimumLength = 1)]
    public string AddressLine1 { get; set; } = "";

    [StringLength(250)]
    public string? AddressLine2 { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = "";

    [StringLength(100)]
    public string? StateOrProvince { get; set; }

    [StringLength(50)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? PhoneNumber { get; set; }
}

public sealed record PrimaryOwnerPersonalKycProfileDto(
    Guid CustomerProfileId,
    string CountryCode,
    KycStatus KycStatus,
    bool ReadyToStartKyc);
