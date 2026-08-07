using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public class ComplianceCaseNote : BaseEntity
{
    public Guid ComplianceCaseId { get; set; }
    public ComplianceCase ComplianceCase { get; set; } = null!;

    public string Note { get; set; } = "";
    public bool IsInternal { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
}
