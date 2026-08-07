using KorridorX.Models.Enums;
using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class SupportOptions
{
    public const string SectionName = "Support";
    public bool SlaWorkerEnabled { get; set; } = false;
    public int SlaWorkerIntervalMinutes { get; set; } = 5;
    public int InvestigationDueHours { get; set; } = 48;
    public Dictionary<SupportTicketPriority, SupportSlaTargetOptions> SlaTargets { get; set; } = new()
    {
        [SupportTicketPriority.Low] = new() { FirstResponseMinutes = 480, ResolutionMinutes = 4320 },
        [SupportTicketPriority.Normal] = new() { FirstResponseMinutes = 240, ResolutionMinutes = 2880 },
        [SupportTicketPriority.High] = new() { FirstResponseMinutes = 60, ResolutionMinutes = 1440 },
        [SupportTicketPriority.Urgent] = new() { FirstResponseMinutes = 15, ResolutionMinutes = 240 }
    };
}

public sealed class SupportSlaTargetOptions
{
    public int FirstResponseMinutes { get; set; }
    public int ResolutionMinutes { get; set; }
}

public sealed class SupportOptionsValidator : IValidateOptions<SupportOptions>
{
    public ValidateOptionsResult Validate(string? name, SupportOptions options)
    {
        var errors = new List<string>();
        if (options.SlaWorkerIntervalMinutes <= 0) errors.Add("Support:SlaWorkerIntervalMinutes must be greater than zero.");
        if (options.InvestigationDueHours <= 0) errors.Add("Support:InvestigationDueHours must be greater than zero.");
        foreach (var priority in Enum.GetValues<SupportTicketPriority>())
        {
            if (!options.SlaTargets.TryGetValue(priority, out var target) || target.FirstResponseMinutes <= 0 || target.ResolutionMinutes <= 0)
                errors.Add($"Support:SlaTargets:{priority} must define positive response and resolution minutes.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
