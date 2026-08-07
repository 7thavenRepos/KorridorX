using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class OpenSanctionsOptions
{
    public const string SectionName = "OpenSanctions";

    public bool IsEnabled { get; set; } = false;
    public string BaseUrl { get; set; } = "https://api.opensanctions.org";
    public string ApiKey { get; set; } = "";
    public string Dataset { get; set; } = "default";
    public string MatchEndpoint { get; set; } = "match";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaximumResults { get; set; } = 5;
    public int RetryCount { get; set; } = 2;
    public int RetryBaseDelayMilliseconds { get; set; } = 500;
    public bool BlockPepMatches { get; set; } = false;
    public string[] IncludeDatasets { get; set; } = [];
    public string[] ExcludeDatasets { get; set; } = [];
}

public sealed class OpenSanctionsOptionsValidator : IValidateOptions<OpenSanctionsOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenSanctionsOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            errors.Add("OpenSanctions:BaseUrl must be a valid absolute URL.");
        if (string.IsNullOrWhiteSpace(options.Dataset))
            errors.Add("OpenSanctions:Dataset is required.");
        if (string.IsNullOrWhiteSpace(options.MatchEndpoint))
            errors.Add("OpenSanctions:MatchEndpoint is required.");
        if (options.TimeoutSeconds is < 5 or > 120)
            errors.Add("OpenSanctions:TimeoutSeconds must be between 5 and 120.");
        if (options.MaximumResults is < 1 or > 50)
            errors.Add("OpenSanctions:MaximumResults must be between 1 and 50.");
        if (options.RetryCount is < 0 or > 5)
            errors.Add("OpenSanctions:RetryCount must be between 0 and 5.");
        if (options.RetryBaseDelayMilliseconds is < 100 or > 10000)
            errors.Add("OpenSanctions:RetryBaseDelayMilliseconds must be between 100 and 10000.");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
