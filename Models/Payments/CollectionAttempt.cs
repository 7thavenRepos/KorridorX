using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Payments;

public class CollectionAttempt : BaseEntity
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public ProviderRequestStatus Status { get; set; } = ProviderRequestStatus.Pending;

    public string? ProviderRequestId { get; set; }
    public string? ProviderResponseId { get; set; }

    public string? RequestPayloadJson { get; set; }
    public string? ResponsePayloadJson { get; set; }

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}