namespace KorridorX.Configuration;

// W2B1 is deliberately non-executable in Staging/Production, even if a
// deployment accidentally enables the flag. W2B2/W4 must explicitly review
// and remove that environment restriction after provider recovery is complete.
public sealed class WalletWithdrawalProcessingOptions
{
    public const string SectionName = "WalletWithdrawalProcessing";
    public bool Enabled { get; set; } = false;
    // Independent hard-off switch: enabling W2B1 reservation APIs must never
    // silently start money movement in the W2B2 background worker.
    public bool DispatchEnabled { get; set; } = false;
    // Reconciliation may settle previously submitted payouts but never submits
    // a payout. It is also Development-only until the W4 acceptance gate.
    public bool RecoveryEnabled { get; set; } = false;
    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 30;
}
