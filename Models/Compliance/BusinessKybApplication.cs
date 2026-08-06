using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class BusinessKybApplication : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public KybStatus Status { get; set; } = KybStatus.Pending;
    public BusinessKybScope KybScope { get; set; } = BusinessKybScope.Full;

    public string? ProviderApplicationId { get; set; }
    public string? SubmittedPayloadJson { get; set; }
    public string? ProviderResponseJson { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public ICollection<BusinessBeneficialOwner> Owners { get; set; } = new List<BusinessBeneficialOwner>();
    public ICollection<BusinessKybDocument> Documents { get; set; } = new List<BusinessKybDocument>();
}
