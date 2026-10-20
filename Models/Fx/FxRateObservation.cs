using KorridorX.Models.Common;

namespace KorridorX.Models.Fx;

/// <summary>Immutable successful provider observation; provider time is never replaced with server time.</summary>
public sealed class FxRateObservation : BaseEntity
{
    public Guid ConfigurationId { get; set; }
    public long ConfigurationRevision { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderRateId { get; set; } = "";
    public string ConnectionKey { get; set; } = "";
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public decimal ProviderRate { get; set; }
    public decimal CustomerRate { get; set; }
    public decimal MarkupPercentage { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProviderTimestampUtc { get; set; }
    public Guid? ProviderRequestLogId { get; set; }
    public bool SendEnabled { get; set; }
    public bool SwapEnabled { get; set; }
    public string Purpose { get; set; } = "";
}
