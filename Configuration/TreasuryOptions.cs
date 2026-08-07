using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class TreasuryOptions
{
    public const string SectionName = "Treasury";
    public bool WalletSyncWorkerEnabled { get; set; } = false;
    public int WalletSyncIntervalMinutes { get; set; } = 5;
    public int ProviderWalletStaleMinutes { get; set; } = 15;
    public int FxRateStaleMinutes { get; set; } = 60;
    public decimal SettlementVarianceTolerance { get; set; } = 0.01m;
}

public sealed class TreasuryOptionsValidator : IValidateOptions<TreasuryOptions>
{
    public ValidateOptionsResult Validate(string? name, TreasuryOptions options)
    {
        var errors = new List<string>();
        if (options.WalletSyncIntervalMinutes < 1) errors.Add("Treasury:WalletSyncIntervalMinutes must be at least 1.");
        if (options.ProviderWalletStaleMinutes < 1) errors.Add("Treasury:ProviderWalletStaleMinutes must be at least 1.");
        if (options.FxRateStaleMinutes < 1) errors.Add("Treasury:FxRateStaleMinutes must be at least 1.");
        if (options.SettlementVarianceTolerance < 0) errors.Add("Treasury:SettlementVarianceTolerance cannot be negative.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
