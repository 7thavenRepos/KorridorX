using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public bool WorkerEnabled { get; set; } = false;
    public int IntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 250;
}

public sealed class DataRetentionOptionsValidator : IValidateOptions<DataRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, DataRetentionOptions options)
    {
        var errors = new List<string>();
        if (options.IntervalHours is < 1 or > 720)
            errors.Add("DataRetention:IntervalHours must be between 1 and 720.");
        if (options.BatchSize is < 1 or > 5000)
            errors.Add("DataRetention:BatchSize must be between 1 and 5000.");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
