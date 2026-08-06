using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class KycApplication : AuditableEntity
{
    public Guid KycProfileId { get; set; }
    public KycProfile KycProfile { get; set; } = null!;

    public KycStatus Status { get; set; } = KycStatus.Pending;

    public string? ProviderApplicationId { get; set; }

    public string? IdentityType { get; set; }
    public string? IdentityNumberLastFour { get; set; }
    public DateTime? IdentityIssueDate { get; set; }
    public DateTime? IdentityExpiryDate { get; set; }

    public string? SubmittedPayloadJson { get; set; }
    public string? ProviderResponseJson { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public ICollection<KycDocument> Documents { get; set; } = new List<KycDocument>();
}
