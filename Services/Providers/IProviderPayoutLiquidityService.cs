namespace KorridorX.Services.Providers;

// A provider-neutral capacity check. Every provider adapter reads its own
// current payout-wallet balance; cached treasury observations are not sufficient.
public interface IProviderPayoutLiquidityService
{
    Task EnsureAvailableAsync(string providerCode, string currencyCode, decimal requiredAmount,
        string? preferredProviderWalletId = null, CancellationToken ct = default);
}

public sealed record ProviderPayoutWalletBalance(
    string ProviderWalletId, string CurrencyCode, decimal AvailableBalance, bool IsActive);

public interface IProviderPayoutWalletBalanceReader
{
    string ProviderCode { get; }
    Task<ProviderPayoutWalletBalance> ReadAsync(string providerWalletId, CancellationToken ct = default);
}

public static class ProviderPayoutLiquidityPolicy
{
    public static void EnsureSufficient(ProviderPayoutWalletBalance snapshot,
        string selectedProviderWalletId, string destinationCurrency, decimal requiredAmount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(selectedProviderWalletId) ||
            string.IsNullOrWhiteSpace(destinationCurrency) || requiredAmount <= 0m)
            throw new InvalidOperationException("A valid payout wallet, currency and positive payout amount are required.");
        if (!snapshot.IsActive ||
            !string.Equals(snapshot.ProviderWalletId, selectedProviderWalletId, StringComparison.Ordinal) ||
            !string.Equals(snapshot.CurrencyCode, destinationCurrency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected provider payout wallet is inactive or does not match the payout currency.");
        if (snapshot.AvailableBalance < 0m || snapshot.AvailableBalance < requiredAmount)
            throw new InvalidOperationException("Provider payout-wallet liquidity is insufficient. The payout has not been submitted.");
    }
}
