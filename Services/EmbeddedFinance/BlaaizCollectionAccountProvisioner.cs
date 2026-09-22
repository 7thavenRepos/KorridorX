using KorridorX.Data;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Microsoft.EntityFrameworkCore;
using KorridorX.Services.Providers;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class BlaaizCollectionAccountProvisioner : ICollectionAccountProvisioner
{
    private readonly AppDbContext _db;
    private readonly IBlaaizApiClient _blaaiz;
    private readonly IProviderWalletResolver _wallets;

    public BlaaizCollectionAccountProvisioner(AppDbContext db, IBlaaizApiClient blaaiz, IProviderWalletResolver wallets)
    {
        _db = db;
        _blaaiz = blaaiz;
        _wallets = wallets;
    }

    public string ProviderCode => KorridorX.Models.Enums.ProviderCode.Blaaiz.ToString();

    public bool Supports(string countryCode, string assetCode) =>
        assetCode.ToUpperInvariant() is "CAD" or "EUR" or "GBP";

    public async Task<CollectionAccountProvisioningResult> ProvisionAsync(
        CollectionAccountProvisioningRequest request,
        CancellationToken ct = default)
    {
        if (!Supports(request.CountryCode, request.AssetCode))
            throw new InvalidOperationException(
                $"Blaaiz international bank accounts are not enabled for asset '{request.AssetCode}'.");

        var providerCustomer = await _db.ProviderCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == KorridorX.Models.Enums.ProviderCode.Blaaiz &&
                x.BusinessCustomerId == request.BusinessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "This business customer has not completed Blaaiz provider onboarding. " +
                "Create/verify the Blaaiz provider customer before provisioning the collection account.");

        if (request.AssetCode.ToUpperInvariant() is "EUR" or "GBP" &&
            !string.Equals(providerCustomer.ProviderStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Blaaiz customer must be VERIFIED before requesting an EUR/GBP account.");

        var legacyMappingExists = await _db.ProviderAccountMappings.AsNoTracking().AnyAsync(x =>
            x.CollectionAccountId == request.CollectionAccountId && !x.IsDeleted && x.ProviderAccountId != null, ct);
        var customerIdText = request.BusinessCustomerId.ToString();
        var previousProviderRequest = await _db.ProviderRequestLogs.AsNoTracking().AnyAsync(x =>
            x.ProviderCode == KorridorX.Models.Enums.ProviderCode.Blaaiz &&
            x.Endpoint == "/api/external/virtual-bank-account" && x.HttpMethod == "POST" &&
            x.RequestBodyJson != null && x.RequestBodyJson.Contains(customerIdText) &&
            _db.ProviderWalletConfigurations.Any(w => w.ProviderCode == "BLAAIZ" &&
                w.AssetCode == request.AssetCode && x.RequestBodyJson.Contains(w.ProviderWalletId)), ct);
        var walletId = await _wallets.SelectAsync("CollectionAccount", request.CollectionAccountId, ProviderCode,
            request.AssetCode, null, ProviderWalletResolver.Collection, legacyMappingExists || previousProviderRequest, ct);
        string raw;

        try
        {
            var created = await _blaaiz.CreateVirtualBankAccountAsync(
                new BlaaizVirtualBankAccountRequest
                {
                    WalletId = walletId,
                    CustomerId = providerCustomer.ProviderCustomerId
                },
                request.BusinessCustomerId,
                ct);

            raw = created.RawResponseJson;
        }
        catch (ProviderIntegrationException ex)
            when (ex.Message.Contains(
                "virtual bank account already exists",
                StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _blaaiz.GetVirtualBankAccountsAsync(
                walletId,
                providerCustomer.ProviderCustomerId,
                request.BusinessCustomerId,
                ct);

            raw = existing.RawResponseJson;
        }

        var account = BlaaizVirtualAccountState.ParseResponse(raw, walletId, providerCustomer.ProviderCustomerId, request.AssetCode);

        return new CollectionAccountProvisioningResult(
            providerCustomer.ProviderCustomerId,
            account.Id,
            account.Reference,
            account.AccountNumber,
            account.AccountName,
            account.BankName,
            account.Metadata(walletId),
            account.Status,
            account.FailureReason);
    }

    // Retained for existing contract checks of wallet-scoped provider responses.
    private static BlaaizVirtualAccountState ParseVirtualAccount(string raw, string walletId) =>
        BlaaizVirtualAccountState.ParseResponse(raw, walletId);
}
