using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class NotificationDeliveryOptionsValidator : IValidateOptions<NotificationDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationDeliveryOptions options)
    {
        var errors = new List<string>();

        if (options.PollIntervalSeconds < 5)
            errors.Add("NotificationDelivery:PollIntervalSeconds must be at least 5.");
        if (options.BatchSize is < 1 or > 200)
            errors.Add("NotificationDelivery:BatchSize must be between 1 and 200.");
        if (options.MaxAttempts is < 1 or > 20)
            errors.Add("NotificationDelivery:MaxAttempts must be between 1 and 20.");
        if (options.RetryBaseMinutes is < 1 or > 1440)
            errors.Add("NotificationDelivery:RetryBaseMinutes must be between 1 and 1440.");
        if (options.ProcessingTimeoutMinutes is < 1 or > 1440)
            errors.Add("NotificationDelivery:ProcessingTimeoutMinutes must be between 1 and 1440.");

        if (options.WorkerEnabled && !options.Smtp.IsEnabled)
            errors.Add("NotificationDelivery:Smtp:IsEnabled must be true when the delivery worker is enabled.");

        if (options.Smtp.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(options.Smtp.Host))
                errors.Add("NotificationDelivery:Smtp:Host is required when SMTP is enabled.");
            if (string.IsNullOrWhiteSpace(options.Smtp.FromAddress))
                errors.Add("NotificationDelivery:Smtp:FromAddress is required when SMTP is enabled.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
