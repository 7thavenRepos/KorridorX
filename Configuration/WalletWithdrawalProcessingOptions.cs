namespace KorridorX.Configuration;

// W2B1 is deliberately non-executable in Staging/Production, even if a
// deployment accidentally enables the flag. W2B2/W4 must explicitly review
// and remove that environment restriction after provider recovery is complete.
public sealed class WalletWithdrawalProcessingOptions
{
    public const string SectionName = "WalletWithdrawalProcessing";
    public bool Enabled { get; set; } = false;
}
