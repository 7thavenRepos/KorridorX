using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class AccountingOptions
{
    public const string SectionName = "Accounting";
    public bool SyncWorkerEnabled { get; set; }
    public int SyncIntervalMinutes { get; set; } = 15;
    public int LookbackDays { get; set; } = 31;
    public string BaseCurrencyCode { get; set; } = "USD";
}

public sealed class AccountingOptionsValidator : IValidateOptions<AccountingOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountingOptions options)
    {
        if (options.SyncIntervalMinutes < 1 || options.SyncIntervalMinutes > 1440)
            return ValidateOptionsResult.Fail("Accounting:SyncIntervalMinutes must be between 1 and 1440.");
        if (options.LookbackDays < 1 || options.LookbackDays > 366)
            return ValidateOptionsResult.Fail("Accounting:LookbackDays must be between 1 and 366.");
        if (string.IsNullOrWhiteSpace(options.BaseCurrencyCode) || options.BaseCurrencyCode.Trim().Length is < 3 or > 10)
            return ValidateOptionsResult.Fail("Accounting:BaseCurrencyCode must contain a valid currency code.");
        return ValidateOptionsResult.Success;
    }
}
