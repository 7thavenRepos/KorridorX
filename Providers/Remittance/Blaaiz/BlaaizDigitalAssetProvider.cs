using System.Globalization;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Remittance.Blaaiz;

public sealed class BlaaizDigitalAssetProvider :
    IDigitalAssetProvider,
    IDigitalAssetBalanceProvider,
    IDigitalAssetHealthProvider,
    IDigitalAssetCollectionProvider
{
    public const string Code = "BLAAIZ";

    private static readonly HashSet<string> SupportedTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "USDT",
            "USDC"
        };

    private readonly AppDbContext _db;
    private readonly IBlaaizApiClient _api;
    private readonly BlaaizOptions _options;

    public BlaaizDigitalAssetProvider(
        AppDbContext db,
        IBlaaizApiClient api,
        IOptions<BlaaizOptions> options)
    {
        _db = db;
        _api = api;
        _options = options.Value;
    }

    public string ProviderCode => Code;

    public bool Supports(string assetCode, string networkCode)
    {
        if (string.IsNullOrWhiteSpace(assetCode) ||
            string.IsNullOrWhiteSpace(networkCode) ||
            !SupportedTokens.Contains(assetCode.Trim()))
        {
            return false;
        }

        return TryMapNetwork(networkCode, out _);
    }

    public Task<DigitalAssetDepositAddressResult> CreateDepositAddressAsync(
        DigitalAssetDepositAddressRequest request,
        CancellationToken ct = default)
    {
        throw new InvalidOperationException(
            "Blaaiz uses amount-specific crypto collection intents for customer deposits. " +
            "Use the digital-asset deposit-intents endpoint instead of persistent deposit-address provisioning.");
    }

    public async Task<DigitalAssetCollectionIntentResult> CreateCollectionIntentAsync(
        DigitalAssetCollectionIntentRequest request,
        CancellationToken ct = default)
    {
        if (request.Amount < 0.1m)
            throw new InvalidOperationException("Blaaiz crypto collection amount must be at least 0.1.");

        var assetCode = Required(request.AssetCode, "Asset code").ToUpperInvariant();
        if (!SupportedTokens.Contains(assetCode))
            throw new InvalidOperationException($"Blaaiz crypto collections currently support USDT and USDC; '{assetCode}' is not supported.");

        var providerNetwork = MapNetwork(request.NetworkCode);
        var providerCustomerId = await ResolveVerifiedProviderCustomerIdAsync(
            request.BusinessProfileId,
            request.BusinessCustomerId,
            ct);
        var wallet = await ResolveWalletAsync(assetCode, ct);

        var response = await _api.InitiateCryptoCollectionAsync(
            new BlaaizCryptoCollectionRequest
            {
                Amount = request.Amount,
                WalletId = wallet.Id,
                Network = providerNetwork,
                Token = assetCode,
                CustomerId = providerCustomerId
            },
            request.DepositIntentId,
            ct);

        var transaction = response.Data.Transaction;
        var providerAmount = transaction.TokenAmount;
        if (providerAmount != request.Amount)
            throw new InvalidOperationException("Blaaiz crypto collection amount does not match the requested KorridorX deposit amount.");

        if (!string.Equals(transaction.Token, assetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Blaaiz crypto collection token does not match the requested KorridorX asset.");

        if (!string.Equals(transaction.Network, providerNetwork, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Blaaiz crypto collection network does not match the requested KorridorX network mapping.");

        return new DigitalAssetCollectionIntentResult(
            Required(wallet.Id, "Blaaiz crypto wallet ID"),
            Required(transaction.TransactionId, "Blaaiz crypto collection transaction ID"),
            Clean(transaction.Reference),
            Required(transaction.Address, "Blaaiz crypto collection address"),
            Required(transaction.Network, "Blaaiz crypto collection network"),
            providerAmount,
            Required(transaction.Status, "Blaaiz crypto collection status"),
            transaction.ExpiresAt);
    }

    public async Task<DigitalAssetWithdrawalSubmissionResult> SubmitWithdrawalAsync(
        DigitalAssetWithdrawalSubmissionRequest request,
        CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new InvalidOperationException("Digital-asset withdrawal amount must be greater than zero.");

        var assetCode = Required(request.AssetCode, "Asset code").ToUpperInvariant();
        if (!SupportedTokens.Contains(assetCode))
            throw new InvalidOperationException($"Blaaiz crypto payouts currently support USDT and USDC; '{assetCode}' is not supported.");

        var providerNetwork = MapNetwork(request.NetworkCode);

        var withdrawal = await _db.DigitalAssetWithdrawals.AsNoTracking()
            .Where(x => x.Id == request.WithdrawalId && !x.IsDeleted)
            .Select(x => new
            {
                x.BusinessProfileId,
                x.BusinessCustomerId,
                x.PayoutId
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Digital-asset withdrawal was not found for Blaaiz payout submission.");

        var providerCustomerId = await ResolveVerifiedProviderCustomerIdAsync(
            withdrawal.BusinessProfileId,
            withdrawal.BusinessCustomerId,
            ct);
        var wallet = await ResolveWalletAsync(assetCode, ct);
        var idempotencyKey = Required(request.ExternalReference, "External reference");

        var response = await _api.InitiateCryptoPayoutAsync(
            new BlaaizCryptoPayoutRequest
            {
                CustomerId = providerCustomerId,
                WalletId = wallet.Id,
                Amount = FormatAmount(request.Amount),
                Address = Required(request.Address, "Destination address"),
                Network = providerNetwork,
                Token = assetCode
            },
            idempotencyKey,
            withdrawal.PayoutId,
            ct);

        var transaction = response.Data.Data;

        return new DigitalAssetWithdrawalSubmissionResult(
            Required(transaction.Id, "Blaaiz crypto transaction ID"),
            Clean(transaction.Reference),
            null,
            Required(transaction.Status, "Blaaiz crypto payout status"));
    }

    public async Task<IReadOnlyList<DigitalAssetProviderBalanceSnapshot>> GetBalancesAsync(
        CancellationToken ct = default)
    {
        var response = await _api.ListCryptoWalletsAsync(ct);

        return response.Data.Data
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Id) &&
                !string.IsNullOrWhiteSpace(x.Asset.Symbol) &&
                SupportedTokens.Contains(x.Asset.Symbol))
            .Select(x => new DigitalAssetProviderBalanceSnapshot(
                x.Id.Trim(),
                x.Asset.Symbol.Trim().ToUpperInvariant(),
                null,
                ParseDecimal(x.Balance, $"Blaaiz balance for wallet '{x.Id}'"),
                x.IsActive,
                null))
            .ToList();
    }

    public async Task<DigitalAssetProviderHealthResult> CheckHealthAsync(
        CancellationToken ct = default)
    {
        try
        {
            var response = await _api.ListCryptoWalletsAsync(ct);
            return new DigitalAssetProviderHealthResult(
                true,
                $"Blaaiz Crypto API reachable. Business crypto wallets discovered: {response.Data.Data.Count}.");
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "Blaaiz Crypto API health check failed."
                : ex.Message.Trim();

            if (message.Length > 900)
                message = message[..900];

            return new DigitalAssetProviderHealthResult(
                false,
                $"Blaaiz Crypto API health check failed: {message}");
        }
    }

    private async Task<string> ResolveVerifiedProviderCustomerIdAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        CancellationToken ct)
    {
        var providerCustomer = await _db.ProviderCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == KorridorX.Models.Enums.ProviderCode.Blaaiz &&
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("The business customer does not have a Blaaiz provider-customer mapping.");

        if (!string.Equals(providerCustomer.ProviderStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Blaaiz business customer must be VERIFIED before crypto activity can be initiated.");

        return Required(providerCustomer.ProviderCustomerId, "Blaaiz provider customer ID");
    }

    private async Task<BlaaizCryptoWalletData> ResolveWalletAsync(
        string assetCode,
        CancellationToken ct)
    {
        var response = await _api.ListCryptoWalletsAsync(ct);
        var candidates = response.Data.Data
            .Where(x =>
                x.IsActive &&
                string.Equals(x.Asset.Symbol?.Trim(), assetCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (_options.CryptoWalletIds.TryGetValue(assetCode, out var configuredWalletId) &&
            !string.IsNullOrWhiteSpace(configuredWalletId))
        {
            var configured = candidates.FirstOrDefault(x =>
                string.Equals(x.Id, configuredWalletId.Trim(), StringComparison.OrdinalIgnoreCase));

            return configured
                ?? throw new InvalidOperationException(
                    $"Configured Blaaiz crypto wallet '{configuredWalletId}' for {assetCode} was not returned as an active matching wallet.");
        }

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException($"No active Blaaiz crypto wallet was found for {assetCode}. Provision the crypto wallet before enabling this rail."),
            _ => throw new InvalidOperationException($"Multiple active Blaaiz crypto wallets were found for {assetCode}. Configure Blaaiz:CryptoWalletIds:{assetCode} explicitly.")
        };
    }

    private string MapNetwork(string networkCode)
    {
        if (!TryMapNetwork(networkCode, out var providerNetwork))
            throw new InvalidOperationException($"KorridorX network '{networkCode}' is not mapped to a Blaaiz crypto network.");

        return providerNetwork;
    }

    private bool TryMapNetwork(string networkCode, out string providerNetwork)
    {
        providerNetwork = "";
        if (string.IsNullOrWhiteSpace(networkCode)) return false;

        var clean = networkCode.Trim().ToUpperInvariant();
        if (_options.CryptoNetworkMappings.TryGetValue(clean, out var mapped) &&
            !string.IsNullOrWhiteSpace(mapped))
        {
            providerNetwork = mapped.Trim().ToUpperInvariant();
            return true;
        }

        if (clean.EndsWith("_MAINNET", StringComparison.Ordinal))
        {
            providerNetwork = clean;
            return true;
        }

        return false;
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##################", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value, string label)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"{label} is not a valid decimal amount.");

        return parsed;
    }

    private static string Required(string? value, string label)
    {
        var clean = Clean(value);
        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException($"{label} is required.")
            : clean;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
