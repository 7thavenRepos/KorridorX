using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public sealed class DataRetentionPolicy : AuditableEntity
{
    public string Name { get; set; } = "";
    public RetentionRecordType RecordType { get; set; }
    public int RetentionDays { get; set; }
    public RetentionAction Action { get; set; } = RetentionAction.ReviewOnly;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
