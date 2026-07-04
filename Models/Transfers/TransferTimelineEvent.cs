using KorridorX.Models.Common;

namespace KorridorX.Models.Transfers;

public class TransferTimelineEvent : BaseEntity
{
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;

    public string EventType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}