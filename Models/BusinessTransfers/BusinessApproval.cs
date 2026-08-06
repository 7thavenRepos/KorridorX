using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.BusinessTransfers;

public class BusinessApproval : BaseEntity
{
    public Guid BusinessProfileId { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public Guid? BusinessPaymentBatchId { get; set; }
    public BusinessPaymentBatch? BusinessPaymentBatch { get; set; }

    public Guid ActionedByUserId { get; set; }
    public ApplicationUser ActionedByUser { get; set; } = null!;

    public BusinessApprovalAction Action { get; set; }
    public string? Comment { get; set; }
    public DateTime ActionedAt { get; set; } = DateTime.UtcNow;
}
