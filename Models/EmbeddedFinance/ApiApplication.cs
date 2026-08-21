using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class ApiApplication : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public EmbeddedFinanceScope Scopes { get; set; } = EmbeddedFinanceScope.None;
    public ApiApplicationStatus Status { get; set; } = ApiApplicationStatus.Active;
    public string? AllowedIpRanges { get; set; }
    public DateTime? LastAuthenticatedAt { get; set; }
    public ICollection<ApiCredential> Credentials { get; set; } = new List<ApiCredential>();
    public ICollection<EmbeddedApiIdempotencyRecord> IdempotencyRecords { get; set; } = new List<EmbeddedApiIdempotencyRecord>();
}
