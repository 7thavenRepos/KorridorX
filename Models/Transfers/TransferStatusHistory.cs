using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Transfers;

public class TransferStatusHistory : BaseEntity
{
    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;

    public TransferStatus OldStatus { get; set; }
    public TransferStatus NewStatus { get; set; }

    public string? Reason { get; set; }
    public string? Source { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}