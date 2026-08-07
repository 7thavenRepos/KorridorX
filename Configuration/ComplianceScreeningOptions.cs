using KorridorX.Models.Enums;
using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class ComplianceScreeningOptions
{
    public const string SectionName = "ComplianceScreening";

    public bool IsEnabled { get; set; } = false;
    public string ProviderCode { get; set; } = "ConfiguredWatchlist";
    public bool RequireRecentClearScreeningForPayout { get; set; } = false;
    public bool FailClosedOnProviderError { get; set; } = true;
    public bool BlockDeclaredPep { get; set; } = true;
    public int ScreeningValidityDays { get; set; } = 30;
    public decimal PotentialMatchScore { get; set; } = 80m;
    public decimal BlockingMatchScore { get; set; } = 95m;
    public bool RescreeningWorkerEnabled { get; set; } = false;
    public int RescreeningIntervalHours { get; set; } = 24;
    public int RescreeningBatchSize { get; set; } = 50;
    public TransactionMonitoringOptions TransactionMonitoring { get; set; } = new();
    public List<ConfiguredWatchlistEntry> Entries { get; set; } = [];
}

public sealed class ConfiguredWatchlistEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public WatchlistType WatchlistType { get; set; } = WatchlistType.Sanctions;
    public string ListName { get; set; } = "Configured Watchlist";
    public string? CountryCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsBlocking { get; set; } = true;
}

public sealed class TransactionMonitoringOptions
{
    public bool IsEnabled { get; set; } = true;
    public int ReviewScore { get; set; } = 40;
    public int BlockScore { get; set; } = 70;
    public int StructuringWindowHours { get; set; } = 24;
    public int StructuringTransferCount { get; set; } = 3;
    public decimal StructuringLowerPercentage { get; set; } = 0.75m;
    public int NewCorridorLookbackDays { get; set; } = 90;
    public int MultipleBeneficiariesWindowHours { get; set; } = 24;
    public int MultipleBeneficiariesCount { get; set; } = 4;
    public Dictionary<string, decimal> RollingAmountThresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 10_000m,
        ["CAD"] = 10_000m,
        ["GBP"] = 8_000m,
        ["EUR"] = 8_000m,
        ["NGN"] = 10_000_000m
    };
}

public sealed class ComplianceScreeningOptionsValidator : IValidateOptions<ComplianceScreeningOptions>
{
    public ValidateOptionsResult Validate(string? name, ComplianceScreeningOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ProviderCode))
            errors.Add("ComplianceScreening:ProviderCode is required.");
        if (options.RequireRecentClearScreeningForPayout && !options.IsEnabled)
            errors.Add("ComplianceScreening must be enabled when RequireRecentClearScreeningForPayout is true.");
        if (options.ScreeningValidityDays is < 1 or > 365)
            errors.Add("ComplianceScreening:ScreeningValidityDays must be between 1 and 365.");
        if (options.PotentialMatchScore is < 0 or > 100)
            errors.Add("ComplianceScreening:PotentialMatchScore must be between 0 and 100.");
        if (options.BlockingMatchScore < options.PotentialMatchScore || options.BlockingMatchScore > 100)
            errors.Add("ComplianceScreening:BlockingMatchScore must be between PotentialMatchScore and 100.");
        if (options.RescreeningIntervalHours is < 1 or > 720)
            errors.Add("ComplianceScreening:RescreeningIntervalHours must be between 1 and 720.");
        if (options.RescreeningBatchSize is < 1 or > 500)
            errors.Add("ComplianceScreening:RescreeningBatchSize must be between 1 and 500.");

        var monitoring = options.TransactionMonitoring;
        if (monitoring.ReviewScore is < 0 or > 100)
            errors.Add("ComplianceScreening:TransactionMonitoring:ReviewScore must be between 0 and 100.");
        if (monitoring.BlockScore < monitoring.ReviewScore || monitoring.BlockScore > 100)
            errors.Add("ComplianceScreening:TransactionMonitoring:BlockScore must be between ReviewScore and 100.");
        if (monitoring.StructuringWindowHours is < 1 or > 720)
            errors.Add("ComplianceScreening:TransactionMonitoring:StructuringWindowHours must be between 1 and 720.");
        if (monitoring.StructuringTransferCount < 2)
            errors.Add("ComplianceScreening:TransactionMonitoring:StructuringTransferCount must be at least 2.");
        if (monitoring.StructuringLowerPercentage is <= 0 or >= 1)
            errors.Add("ComplianceScreening:TransactionMonitoring:StructuringLowerPercentage must be greater than 0 and less than 1.");
        if (monitoring.NewCorridorLookbackDays is < 1 or > 3650)
            errors.Add("ComplianceScreening:TransactionMonitoring:NewCorridorLookbackDays must be between 1 and 3650.");
        if (monitoring.MultipleBeneficiariesWindowHours is < 1 or > 720)
            errors.Add("ComplianceScreening:TransactionMonitoring:MultipleBeneficiariesWindowHours must be between 1 and 720.");
        if (monitoring.MultipleBeneficiariesCount < 2)
            errors.Add("ComplianceScreening:TransactionMonitoring:MultipleBeneficiariesCount must be at least 2.");

        foreach (var threshold in monitoring.RollingAmountThresholds)
        {
            if (string.IsNullOrWhiteSpace(threshold.Key) || threshold.Value <= 0)
                errors.Add("ComplianceScreening:TransactionMonitoring:RollingAmountThresholds must contain valid currency codes and positive values.");
        }

        var duplicateIds = options.Entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1);
        if (duplicateIds)
            errors.Add("ComplianceScreening:Entries contains duplicate IDs.");

        foreach (var entry in options.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                errors.Add("Every ComplianceScreening entry requires a name.");
            if (string.IsNullOrWhiteSpace(entry.ListName))
                errors.Add("Every ComplianceScreening entry requires a list name.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
