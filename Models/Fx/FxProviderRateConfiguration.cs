using KorridorX.Models.Common;

namespace KorridorX.Models.Fx;

/// <summary>A pricing policy attached to an existing pair, never a second pair catalogue.</summary>
public sealed class FxProviderRateConfiguration : AuditableEntity
{
    public Guid? InstantPairId { get; set; }
    public Guid? MarketplacePairId { get; set; }
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public string? ProviderRateId { get; set; }
    public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public bool IsActive { get; set; }
    public long Revision { get; set; } = 1;
    // Hash only. A connection change requires a new verified activation.
    public string? ActivatedConnectionKey { get; set; }
    public string ScheduleMode { get; set; } = "Interval";
    public int IntervalMinutes { get; set; } = 180;
    public string DailyTimes { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime? NextRefreshAtUtc { get; set; }
    public Guid? RefreshLeaseId { get; set; }
    public DateTime? RefreshLeaseUntilUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? LastObservationStartedAtUtc { get; set; }
    public DateTime? LastProviderTimestampUtc { get; set; }
    public string? LastError { get; set; }
    public decimal? LastProviderRate { get; set; }
    public decimal? LastCustomerRate { get; set; }
}
