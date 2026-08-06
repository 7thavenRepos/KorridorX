using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public class KycDocument : AuditableEntity
{
    public Guid KycApplicationId { get; set; }
    public KycApplication KycApplication { get; set; } = null!;

    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";

    public string StorageProvider { get; set; } = "BlaaizS3";
    public string StorageKey { get; set; } = "";
    public string? StorageUrl { get; set; }

    public string? ProviderFileId { get; set; }
    public string? ProviderDocumentId { get; set; }
    public bool IsUploaded { get; set; }
    public DateTime? UploadConfirmedAt { get; set; }

    public bool IsAttachedToProvider { get; set; }
    public DateTime? AttachedToProviderAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
