using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Common;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Recipients;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Compliance;

public class ComplianceCase : AuditableEntity
{
    public string Reference { get; set; } = "";
    public ComplianceCaseType CaseType { get; set; }
    public ComplianceCaseStatus Status { get; set; } = ComplianceCaseStatus.Open;
    public ComplianceCasePriority Priority { get; set; } = ComplianceCasePriority.Medium;

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsBlocking { get; set; }

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

    public Guid? ScreeningRecordId { get; set; }
    public ScreeningRecord? ScreeningRecord { get; set; }

    public Guid? AmlFlagId { get; set; }
    public AmlFlag? AmlFlag { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }

    public ComplianceCaseDecision? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<ComplianceCaseNote> Notes { get; set; } = new List<ComplianceCaseNote>();
    public ICollection<ComplianceCaseEvidence> Evidence { get; set; } = new List<ComplianceCaseEvidence>();
}
