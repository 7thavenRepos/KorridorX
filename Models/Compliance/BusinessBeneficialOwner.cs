using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public class BusinessBeneficialOwner : AuditableEntity
{
    public Guid BusinessKybApplicationId { get; set; }
    public BusinessKybApplication BusinessKybApplication { get; set; } = null!;

    public string? ProviderOwnerId { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Nationality { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string? Title { get; set; }

    public decimal OwnershipPercentage { get; set; }
    public bool HasControl { get; set; }
    public bool IsSigner { get; set; }
    public bool IsBeneficialOwner { get; set; } = true;
    public bool IsPep { get; set; }

    public string IdDocumentType { get; set; } = "";
    public string IdentityNumberLastFour { get; set; } = "";
    public string IdentityNumberEncrypted { get; set; } = "";
    public string IdDocumentCountry { get; set; } = "";
    public DateTime IdExpiryDate { get; set; }

    public string? IdentityFrontProviderFileId { get; set; }
    public string? IdentityBackProviderFileId { get; set; }
    public bool IsIdentityFrontUploaded { get; set; }
    public bool IsIdentityBackUploaded { get; set; }
    public DateTime? IdentityFrontUploadConfirmedAt { get; set; }
    public DateTime? IdentityBackUploadConfirmedAt { get; set; }
    public bool AreIdentityFilesAttachedToProvider { get; set; }
    public DateTime? IdentityFilesAttachedAt { get; set; }

    public string ProviderStatus { get; set; } = "PENDING";
    public string? ProviderAdminCommentsJson { get; set; }
    public string? RejectionReason { get; set; }
}
