using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.WalletWithdrawals;

namespace KorridorX.Services.WalletWithdrawals;

// Pure validation and fingerprints for deterministic, concurrent idempotency.
public static class WalletWithdrawalInitiationPolicy
{
    public static string HashIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12 || value.Length > 128 ||
            value.Any(x => !(x is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            throw new InvalidOperationException("Idempotency-Key must be 12–128 ASCII letters, digits, '-' or '_'.");
        return Hash(value);
    }

    public static string Fingerprint(Guid walletId, Guid destinationId, Guid channelId, decimal amount, bool business)
    {
        if (walletId == Guid.Empty || destinationId == Guid.Empty || channelId == Guid.Empty || amount <= 0m)
            throw new InvalidOperationException("Valid wallet, destination, payout channel and amount are required.");
        return Hash(string.Join("|", walletId.ToString("N"), destinationId.ToString("N"),
            channelId.ToString("N"), amount.ToString("0.############################", CultureInfo.InvariantCulture),
            business ? "B" : "C"));
    }

    public static void EnsureSame(FiatWalletWithdrawal previous, string fingerprint)
    {
        if (!string.Equals(previous.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Idempotency-Key was already used for a different withdrawal request.");
    }

    public static void EnsureFreshQuote(WalletWithdrawalFeePreviewDto quote, decimal requestedAmount, DateTime nowUtc)
    {
        if (!quote.RequiresFreshQuoteBeforeExecution || quote.WithdrawalAmount != requestedAmount ||
            quote.TotalWalletDebit != requestedAmount || quote.ExpectedRecipientAmount <= 0m ||
            quote.ProviderFee < 0m || quote.KorridorXFee != 0m ||
            quote.KorridorXFeePolicy != "NOT_CONFIGURED" ||
            requestedAmount != quote.ExpectedRecipientAmount + quote.ProviderFee ||
            quote.QuotedAtUtc > nowUtc.AddSeconds(2) || quote.QuotedAtUtc < nowUtc.AddSeconds(-30))
            throw new InvalidOperationException("The withdrawal quote is stale or does not reconcile. Request a fresh fee preview.");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
