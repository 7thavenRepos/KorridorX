using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Recipients;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Compliance;

public class ScreeningRecord : AuditableEntity
{
    public ScreeningSubjectType SubjectType { get; set; }
    public ScreeningReason Reason { get; set; }
    public ScreeningStatus Status { get; set; } = ScreeningStatus.ManualReview;

    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public Guid? BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }

    public Guid? RecipientId { get; set; }
    public Recipient? Recipient { get; set; }

    public Guid? BusinessBeneficiaryId { get; set; }
    public BusinessBeneficiary? BusinessBeneficiary { get; set; }

    public Guid? BusinessBeneficialOwnerId { get; set; }
    public BusinessBeneficialOwner? BusinessBeneficialOwner { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public string SubjectName { get; set; } = "";
    public string? CountryCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? RegistrationNumberLastFour { get; set; }

    public string ProviderCode { get; set; } = "";
    public string? ProviderReference { get; set; }
    public decimal HighestMatchScore { get; set; }
    public bool IsBlocking { get; set; }

    public string? RequestJson { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public ICollection<ScreeningMatch> Matches { get; set; } = new List<ScreeningMatch>();
}
