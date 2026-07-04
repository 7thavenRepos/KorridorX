using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class KycProfile : AuditableEntity
{
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public KycStatus Status { get; set; } = KycStatus.NotStarted;

    public string? ProviderCode { get; set; } = "Blaaiz";
    public string? ProviderKycId { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    public string? RejectionReason { get; set; }

    public ICollection<KycApplication> Applications { get; set; } = new List<KycApplication>();
}