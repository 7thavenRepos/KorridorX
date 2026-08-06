using KorridorX.Models.BusinessTransfers;
using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.BusinessFunding;

public class BusinessWalletReservation : AuditableEntity
{
    public Guid BusinessWalletId { get; set; }
    public BusinessWallet BusinessWallet { get; set; } = null!;

    public Guid TransferId { get; set; }
    public Transfer Transfer { get; set; } = null!;

    public Guid? BusinessPaymentBatchId { get; set; }
    public BusinessPaymentBatch? BusinessPaymentBatch { get; set; }

    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public BusinessWalletReservationStatus Status { get; set; } = BusinessWalletReservationStatus.Active;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CapturedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
