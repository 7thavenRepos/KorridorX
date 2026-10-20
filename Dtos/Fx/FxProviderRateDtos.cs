using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Fx;

public sealed record FxPairOptionDto(string PairType, Guid PairId, string Code,
    string SourceCurrencyCode, string DestinationCurrencyCode, string Status);
public sealed record FxProviderOptionDto(string Code, string DisplayName, bool IsAvailable);
public sealed record FxProviderOptionsDto(IReadOnlyList<FxPairOptionDto> Pairs, IReadOnlyList<FxProviderOptionDto> Providers);

public sealed class FxRateScheduleDto
{
    public string Mode { get; set; } = "Interval";
    public int IntervalMinutes { get; set; } = 180;
    public string[] DailyTimes { get; set; } = [];
    public string TimeZoneId { get; set; } = "UTC";
}

public sealed class SaveFxProviderConfigurationRequestDto
{
    [Required] public string PairType { get; set; } = "";
    public Guid PairId { get; set; }
    [Required, MaxLength(50)] public string ProviderCode { get; set; } = "";
    [MaxLength(150)] public string? ProviderRateId { get; set; }
    public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public FxRateScheduleDto Schedule { get; set; } = new();
    public long? Revision { get; set; }
}

public sealed record FxConfigurationRevisionRequestDto(long Revision);
public sealed record FxProviderPreviewDto(string SourceCurrencyCode, string DestinationCurrencyCode,
    string ProviderCode, string ProviderRateId, decimal ProviderRate, decimal CustomerRate, decimal MarkupRate,
    DateTime RequestedAtUtc, DateTime ReceivedAtUtc, DateTime? ProviderTimestampUtc, bool SendEnabled, bool SwapEnabled);

public sealed class FxProviderConfigurationDto
{
    public Guid Id { get; set; }
    public string PairType { get; set; } = "";
    public Guid PairId { get; set; }
    public string Code { get; set; } = "";
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public string? ProviderRateId { get; set; }
    public decimal MarkupPercentage { get; set; }
    public decimal? MinimumCustomerRate { get; set; }
    public decimal? MaximumCustomerRate { get; set; }
    public FxRateScheduleDto Schedule { get; set; } = new();
    public bool IsActive { get; set; }
    public long Revision { get; set; }
    public DateTime? NextRefreshAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? LastProviderTimestampUtc { get; set; }
    public string? LastError { get; set; }
    public decimal? LastProviderRate { get; set; }
    public decimal? LastCustomerRate { get; set; }
}
