using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class ProviderAccountMapping : AuditableEntity
{
    public Guid CollectionAccountId { get; set; }
    public CollectionAccount CollectionAccount { get; set; } = null!;
    public string ProviderCode { get; set; } = "";
    public string? ProviderCustomerId { get; set; }
    public string? ProviderAccountId { get; set; }
    public string? ProviderReference { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? BankName { get; set; }
    public ProviderAccountMappingStatus Status { get; set; } = ProviderAccountMappingStatus.Pending;
    public string? FailureReason { get; set; }
    public string? MetadataJson { get; set; }
}
