using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Payments;

public class PayoutAttempt : BaseEntity
{
    public Guid PayoutId { get; set; }
    public Payout Payout { get; set; } = null!;

    public ProviderRequestStatus Status { get; set; } = ProviderRequestStatus.Pending;

    public string? ProviderRequestId { get; set; }
    public string? ProviderResponseId { get; set; }

    public string? RequestPayloadJson { get; set; }
    public string? ResponsePayloadJson { get; set; }

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}