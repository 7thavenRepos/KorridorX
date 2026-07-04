namespace KorridorX.Models.Common;

public abstract class AuditableEntity : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public Guid? LastUpdatedByUserId { get; set; }
    public Guid? DeletedByUserId { get; set; }
}