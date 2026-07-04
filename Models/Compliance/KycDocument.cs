using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public class KycDocument : AuditableEntity
{
    public Guid KycApplicationId { get; set; }
    public KycApplication KycApplication { get; set; } = null!;

    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";

    public string StorageProvider { get; set; } = "Local";
    public string StorageKey { get; set; } = "";
    public string? StorageUrl { get; set; }

    public string? ProviderDocumentId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}