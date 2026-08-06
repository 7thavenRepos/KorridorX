using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class BusinessKybDocument : AuditableEntity
{
    public Guid BusinessKybApplicationId { get; set; }
    public BusinessKybApplication BusinessKybApplication { get; set; } = null!;

    public BusinessKybDocumentType DocumentType { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";

    public string StorageProvider { get; set; } = "BlaaizS3";
    public string StorageKey { get; set; } = "";
    public string? ProviderFileId { get; set; }
    public string? ProviderDocumentId { get; set; }

    public bool IsUploaded { get; set; }
    public DateTime? UploadConfirmedAt { get; set; }
    public bool IsRegisteredWithProvider { get; set; }
    public DateTime? RegisteredWithProviderAt { get; set; }

    public string ProviderStatus { get; set; } = "PENDING";
    public string? ProviderAdminCommentsJson { get; set; }
    public string? RejectionReason { get; set; }
}
