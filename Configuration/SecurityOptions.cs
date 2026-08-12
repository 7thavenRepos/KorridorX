using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public static class SecurityRateLimitPolicies
{
    public const string Authentication = "Authentication";
    public const string Sensitive = "Sensitive";
    public const string Webhook = "Webhook";
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public RateLimitOptions RateLimits { get; set; } = new();
    public SessionSecurityOptions Sessions { get; set; } = new();
    public AccountSecurityOptions Accounts { get; set; } = new();
    public MfaSecurityOptions Mfa { get; set; } = new();
    public TransferRiskOptions TransferRisk { get; set; } = new();
}

public sealed class RateLimitOptions
{
    public int GlobalPermitLimit { get; set; } = 300;
    public int GlobalWindowMinutes { get; set; } = 1;
    public int AuthenticationPermitLimit { get; set; } = 10;
    public int AuthenticationWindowMinutes { get; set; } = 1;
    public int SensitivePermitLimit { get; set; } = 60;
    public int SensitiveWindowMinutes { get; set; } = 1;
    public int WebhookPermitLimit { get; set; } = 300;
    public int WebhookWindowMinutes { get; set; } = 1;
}

public sealed class SessionSecurityOptions
{
    public int MaximumActiveSessionsPerUser { get; set; } = 10;
}

public sealed class AccountSecurityOptions
{
    public bool RequireConfirmedEmail { get; set; } = true;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public int TokenLifespanMinutes { get; set; } = 120;
}

public sealed class MfaSecurityOptions
{
    public bool EnforceForPrivilegedRoles { get; set; } = true;
    public int ChallengeLifespanMinutes { get; set; } = 5;
    public int MaximumVerificationAttempts { get; set; } = 5;
    public int RecoveryCodeCount { get; set; } = 10;
    public string Issuer { get; set; } = "KorridorX";
    public string CodeReplayPepper { get; set; } =
        "change-me-development-mfa-code-replay-pepper";
}

public sealed class TransferRiskOptions
{
    public bool IsEnabled { get; set; } = true;
    public int ReviewScore { get; set; } = 40;
    public int BlockScore { get; set; } = 70;
    public int RecentRecipientHours { get; set; } = 24;
    public int RapidTransferWindowMinutes { get; set; } = 60;
    public int RapidTransferCount { get; set; } = 3;
    public int DailyTransferCount { get; set; } = 5;
    public int DuplicateTransferWindowHours { get; set; } = 24;
    public int DuplicateTransferCount { get; set; } = 2;
    public int FailedLoginWindowHours { get; set; } = 24;
    public int FailedLoginCount { get; set; } = 3;
    public int DistinctDeviceWindowDays { get; set; } = 30;
    public int DistinctDeviceCount { get; set; } = 3;
    public Dictionary<string, decimal> HighValueThresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 5000m,
        ["CAD"] = 5000m,
        ["GBP"] = 4000m,
        ["EUR"] = 4000m,
        ["NGN"] = 5_000_000m
    };
}

public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        var errors = new List<string>();

        ValidatePositive(options.RateLimits.GlobalPermitLimit, "Security:RateLimits:GlobalPermitLimit", errors);
        ValidatePositive(options.RateLimits.GlobalWindowMinutes, "Security:RateLimits:GlobalWindowMinutes", errors);
        ValidatePositive(options.RateLimits.AuthenticationPermitLimit, "Security:RateLimits:AuthenticationPermitLimit", errors);
        ValidatePositive(options.RateLimits.AuthenticationWindowMinutes, "Security:RateLimits:AuthenticationWindowMinutes", errors);
        ValidatePositive(options.RateLimits.SensitivePermitLimit, "Security:RateLimits:SensitivePermitLimit", errors);
        ValidatePositive(options.RateLimits.SensitiveWindowMinutes, "Security:RateLimits:SensitiveWindowMinutes", errors);
        ValidatePositive(options.RateLimits.WebhookPermitLimit, "Security:RateLimits:WebhookPermitLimit", errors);
        ValidatePositive(options.RateLimits.WebhookWindowMinutes, "Security:RateLimits:WebhookWindowMinutes", errors);
        ValidatePositive(options.Sessions.MaximumActiveSessionsPerUser, "Security:Sessions:MaximumActiveSessionsPerUser", errors);

        if (!Uri.TryCreate(options.Accounts.FrontendBaseUrl, UriKind.Absolute, out var frontendUri) ||
            (frontendUri.Scheme != Uri.UriSchemeHttp && frontendUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(frontendUri.UserInfo) ||
            !string.IsNullOrEmpty(frontendUri.Query) ||
            !string.IsNullOrEmpty(frontendUri.Fragment))
        {
            errors.Add("Security:Accounts:FrontendBaseUrl must be an absolute HTTP or HTTPS URL without a query or fragment.");
        }

        if (options.Accounts.TokenLifespanMinutes is < 15 or > 1440)
            errors.Add("Security:Accounts:TokenLifespanMinutes must be between 15 and 1440.");

        if (options.Mfa.ChallengeLifespanMinutes is < 1 or > 15)
            errors.Add("Security:Mfa:ChallengeLifespanMinutes must be between 1 and 15.");
        if (options.Mfa.MaximumVerificationAttempts is < 3 or > 10)
            errors.Add("Security:Mfa:MaximumVerificationAttempts must be between 3 and 10.");
        if (options.Mfa.RecoveryCodeCount is < 8 or > 20)
            errors.Add("Security:Mfa:RecoveryCodeCount must be between 8 and 20.");
        if (string.IsNullOrWhiteSpace(options.Mfa.Issuer) || options.Mfa.Issuer.Length > 64)
            errors.Add("Security:Mfa:Issuer is required and must not exceed 64 characters.");
        if (string.IsNullOrWhiteSpace(options.Mfa.CodeReplayPepper) ||
            options.Mfa.CodeReplayPepper.Length < 32)
        {
            errors.Add("Security:Mfa:CodeReplayPepper must contain at least 32 characters.");
        }

        if (options.TransferRisk.ReviewScore < 0 || options.TransferRisk.ReviewScore > 100)
            errors.Add("Security:TransferRisk:ReviewScore must be between 0 and 100.");
        if (options.TransferRisk.BlockScore < options.TransferRisk.ReviewScore || options.TransferRisk.BlockScore > 100)
            errors.Add("Security:TransferRisk:BlockScore must be between ReviewScore and 100.");

        ValidatePositive(options.TransferRisk.RecentRecipientHours, "Security:TransferRisk:RecentRecipientHours", errors);
        ValidatePositive(options.TransferRisk.RapidTransferWindowMinutes, "Security:TransferRisk:RapidTransferWindowMinutes", errors);
        ValidatePositive(options.TransferRisk.RapidTransferCount, "Security:TransferRisk:RapidTransferCount", errors);
        ValidatePositive(options.TransferRisk.DailyTransferCount, "Security:TransferRisk:DailyTransferCount", errors);
        ValidatePositive(options.TransferRisk.DuplicateTransferWindowHours, "Security:TransferRisk:DuplicateTransferWindowHours", errors);
        ValidatePositive(options.TransferRisk.DuplicateTransferCount, "Security:TransferRisk:DuplicateTransferCount", errors);
        ValidatePositive(options.TransferRisk.FailedLoginWindowHours, "Security:TransferRisk:FailedLoginWindowHours", errors);
        ValidatePositive(options.TransferRisk.FailedLoginCount, "Security:TransferRisk:FailedLoginCount", errors);
        ValidatePositive(options.TransferRisk.DistinctDeviceWindowDays, "Security:TransferRisk:DistinctDeviceWindowDays", errors);
        ValidatePositive(options.TransferRisk.DistinctDeviceCount, "Security:TransferRisk:DistinctDeviceCount", errors);

        foreach (var threshold in options.TransferRisk.HighValueThresholds)
        {
            if (string.IsNullOrWhiteSpace(threshold.Key) || threshold.Value <= 0)
                errors.Add("Security:TransferRisk:HighValueThresholds must contain valid currency codes and positive amounts.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidatePositive(int value, string key, ICollection<string> errors)
    {
        if (value <= 0)
            errors.Add($"{key} must be greater than zero.");
    }
}
