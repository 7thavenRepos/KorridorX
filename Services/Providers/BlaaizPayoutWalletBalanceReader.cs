using KorridorX.Providers.Remittance.Blaaiz;

namespace KorridorX.Services.Providers;

public sealed class BlaaizPayoutWalletBalanceReader(IBlaaizApiClient api)
    : IProviderPayoutWalletBalanceReader
{
    public string ProviderCode => "BLAAIZ";

    public async Task<ProviderPayoutWalletBalance> ReadAsync(
        string providerWalletId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerWalletId))
            throw new InvalidOperationException("A selected provider payout wallet is required.");
        var response = await api.GetWalletAsync(providerWalletId, ct);
        if (response.Data is null)
            throw new InvalidOperationException("Provider payout wallet details could not be verified.");
        return new ProviderPayoutWalletBalance(response.Data.Id, response.Data.Currency,
            response.Data.Amount, response.Data.IsActive);
    }
}
