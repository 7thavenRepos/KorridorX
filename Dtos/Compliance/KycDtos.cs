using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public record KycDocumentDto(
    Guid Id,
    KycDocumentType DocumentType,
    string FileName,
    string MimeType,
    bool IsUploaded,
    DateTime? UploadConfirmedAt,
    bool IsAttachedToProvider,
    DateTime? AttachedToProviderAt,
    string? RejectionReason,
    DateTime CreatedAt);

public record KycApplicationDto(
    Guid Id,
    Guid KycProfileId,
    KycStatus Status,
    string? ProviderApplicationId,
    string? IdentityType,
    string? IdentityNumberLastFour,
    DateTime? IdentityIssueDate,
    DateTime? IdentityExpiryDate,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNote,
    IReadOnlyList<KycDocumentDto> Documents,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public record KycStatusDto(
    Guid KycProfileId,
    KycStatus Status,
    string? ProviderCode,
    string? ProviderKycId,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    KycApplicationDto? CurrentApplication);

public class SaveKycIdentityRequestDto
{
    [Required]
    [MaxLength(50)]
    public string IdType { get; set; } = "";

    [Required]
    [MaxLength(150)]
    public string IdNumber { get; set; } = "";

    public DateTime? IdIssueDate { get; set; }
    public DateTime? IdExpiryDate { get; set; }
}

public class CreateKycDocumentUploadUrlRequestDto
{
    [Required]
    public KycDocumentType DocumentType { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = "";

    [Range(1, 26_214_400)]
    public long FileSizeBytes { get; set; }
}

public record KycDocumentUploadUrlDto(
    Guid DocumentId,
    KycDocumentType DocumentType,
    string ProviderFileId,
    string UploadUrl,
    IReadOnlyDictionary<string, string> UploadHeaders);

public class ConfirmKycDocumentUploadRequestDto
{
    [Required]
    [MaxLength(150)]
    public string ProviderFileId { get; set; } = "";
}

public record AdminKycApplicationListItemDto(
    Guid ApplicationId,
    Guid CustomerProfileId,
    string CustomerName,
    string? Email,
    string CountryCode,
    KycStatus Status,
    string? ProviderApplicationId,
    DateTime? SubmittedAt,
    DateTime CreatedAt);

public record AdminKycApplicationDetailsDto(
    Guid CustomerProfileId,
    string CustomerName,
    string? Email,
    string CountryCode,
    KycStatus CustomerKycStatus,
    KycApplicationDto Application);
