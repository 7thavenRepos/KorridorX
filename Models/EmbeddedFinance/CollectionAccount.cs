using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;

namespace KorridorX.Models.EmbeddedFinance;

public class CollectionAccount : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;
    public Guid BusinessCustomerId { get; set; }
    public BusinessCustomer BusinessCustomer { get; set; } = null!;
    public string ExternalReference { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public Asset Asset { get; set; } = null!;
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public CollectionAccountStatus Status { get; set; } = CollectionAccountStatus.Pending;
    public ICollection<ProviderAccountMapping> ProviderMappings { get; set; } = new List<ProviderAccountMapping>();
}
