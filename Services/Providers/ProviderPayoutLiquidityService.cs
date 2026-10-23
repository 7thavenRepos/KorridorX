using KorridorX.Data;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Providers;

// This check is deliberately read-only. It runs BEFORE the outbound payout
// POST and never reserves provider liquidity or pretends the quoted balance
// is a settlement guarantee. The provider remains authoritative at execution.
public sealed class ProviderPayoutLiquidityService(
    AppDbContext db, IProviderWalletResolver resolver,
    IEnumerable<IProviderPayoutWalletBalanceReader> readers) : IProviderPayoutLiquidityService
{
    public async Task EnsureAvailableAsync(string providerCode, string currencyCode, decimal requiredAmount,
        string? preferredProviderWalletId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode) || string.IsNullOrWhiteSpace(currencyCode) ||
            requiredAmount <= 0m)
            throw new InvalidOperationException("A payout provider, currency and positive amount are required.");

        var provider = providerCode.Trim().ToUpperInvariant();
        var currency = currencyCode.Trim().ToUpperInvariant();
        var query = db.ProviderWalletConfigurations.AsNoTracking().Where(x =>
            !x.IsDeleted && x.IsActive && x.PayoutEnabled &&
            x.ProviderCode == provider && x.Environment == resolver.EnvironmentName &&
            x.AssetCode == currency && x.NetworkCode == "" &&
            x.VerifiedAt != null && x.VerifiedConnectionKey == resolver.ConnectionKey);
        if (string.IsNullOrWhiteSpace(preferredProviderWalletId))
            query = query.Where(x => x.DefaultForPayout);
        else
            query = query.Where(x => x.ProviderWalletId == preferredProviderWalletId);

        var wallet = await query.SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "A verified, active provider payout wallet is required for " + currency + ".");

        var adapter = readers.SingleOrDefault(x =>
            string.Equals(x.ProviderCode, provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The selected provider has no payout-liquidity adapter.");

        var balance = await adapter.ReadAsync(wallet.ProviderWalletId, ct);
        ProviderPayoutLiquidityPolicy.EnsureSufficient(balance, wallet.ProviderWalletId,
            currency, requiredAmount);
    }
}
