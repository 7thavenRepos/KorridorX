using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Services.WalletWithdrawals;

// The core withdrawal engine requests a quote from the selected provider,
// not directly from Blaaiz. Adding a provider requires another adapter, not
// new branches in the initiation or settlement engine.
public interface IWalletWithdrawalQuoteProvider
{
    string ProviderCode { get; }
    Task<WalletWithdrawalFeeAmounts> QuoteAsync(string currencyCode, string providerCustomerId,
        decimal walletDebit, int decimalPlaces, PaymentMethod paymentMethod, CancellationToken ct = default);
}

public interface IWalletWithdrawalQuoteGateway
{
    Task<WalletWithdrawalFeeAmounts> QuoteAsync(string providerCode, string currencyCode,
        string providerCustomerId, decimal walletDebit, int decimalPlaces,
        PaymentMethod paymentMethod, CancellationToken ct = default);
}

public sealed class WalletWithdrawalQuoteGateway(IEnumerable<IWalletWithdrawalQuoteProvider> adapters)
    : IWalletWithdrawalQuoteGateway
{
    public Task<WalletWithdrawalFeeAmounts> QuoteAsync(string providerCode, string currencyCode,
        string providerCustomerId, decimal walletDebit, int decimalPlaces,
        PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            throw new InvalidOperationException("The selected payout channel has no provider.");
        var matching = adapters.SingleOrDefault(x =>
            string.Equals(x.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Fee quotations are not implemented for the selected payout provider.");
        return matching.QuoteAsync(currencyCode, providerCustomerId, walletDebit,
            decimalPlaces, paymentMethod, ct);
    }
}

public sealed class BlaaizWalletWithdrawalQuoteProvider(IBlaaizApiClient blaaiz)
    : IWalletWithdrawalQuoteProvider
{
    public string ProviderCode => "BLAAIZ";

    public async Task<WalletWithdrawalFeeAmounts> QuoteAsync(string currencyCode, string providerCustomerId,
        decimal walletDebit, int decimalPlaces, PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        if (!Guid.TryParse(providerCustomerId, out var id) || id == Guid.Empty)
            throw new InvalidOperationException("A valid verified Blaaiz customer ID is required for a payout quote.");
        WalletWithdrawalFeePolicy.ValidateAmount(walletDebit, walletDebit, decimalPlaces);
        var response = await blaaiz.GetFeeBreakdownAsync(new BlaaizFeeBreakdownRequest
        {
            FromCurrencyId = currencyCode, ToCurrencyId = currencyCode,
            FromAmount = walletDebit, CustomerId = id.ToString("D")
        }, ct);
        return WalletWithdrawalFeePolicy.Evaluate(response.Data, paymentMethod,
            walletDebit, decimalPlaces);
    }
}
