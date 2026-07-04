using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Providers;

public class ProviderRequestLog : BaseEntity
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;

    public string Endpoint { get; set; } = "";
    public string HttpMethod { get; set; } = "";

    public string? RequestHeadersJson { get; set; }
    public string? RequestBodyJson { get; set; }

    public ProviderRequestStatus Status { get; set; } = ProviderRequestStatus.Pending;

    public int? ResponseStatusCode { get; set; }
    public string? ResponseBodyJson { get; set; }

    public string? ErrorMessage { get; set; }

    public Guid? RelatedTransferId { get; set; }
    public Guid? RelatedCollectionId { get; set; }
    public Guid? RelatedPayoutId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public long? DurationMs { get; set; }
}