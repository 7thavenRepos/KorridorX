using System.Text.Json;
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
        string.Equals(assetCode, "CAD", StringComparison.OrdinalIgnoreCase);

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
                "Create/verify the Blaaiz provider customer before provisioning the CAD international bank account.");

        var legacyMappingExists = await _db.ProviderAccountMappings.AsNoTracking().AnyAsync(x =>
            x.CollectionAccountId == request.CollectionAccountId && !x.IsDeleted && x.ProviderAccountId != null, ct);
        var customerIdText = request.BusinessCustomerId.ToString();
        var previousProviderRequest = await _db.ProviderRequestLogs.AsNoTracking().AnyAsync(x =>
            x.ProviderCode == KorridorX.Models.Enums.ProviderCode.Blaaiz &&
            x.Endpoint == "/api/external/virtual-bank-account" && x.HttpMethod == "POST" &&
            x.RequestBodyJson != null && x.RequestBodyJson.Contains(customerIdText), ct);
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

        var account = ParseVirtualAccount(raw, walletId);

        return new CollectionAccountProvisioningResult(
            providerCustomer.ProviderCustomerId,
            account.Id,
            account.AccountReference ?? account.ReservationReference,
            account.AccountNumber,
            account.AccountName,
            account.BankName,
            JsonSerializer.Serialize(new
            {
                account.BankCode,
                account.SortCode,
                account.Iban,
                account.Provider,
                account.ReservationReference,
                account.Status,
                providerWalletId = walletId,
                rawResponse = raw
            }));
    }

    private static VirtualAccountData ParseVirtualAccount(string raw, string walletId)
    {
        using var doc = JsonDocument.Parse(raw);
        var data = GetData(doc.RootElement);
        JsonElement item;

        if (data.ValueKind == JsonValueKind.Array)
        {
            var matches = data.EnumerateArray()
                .Where(x =>
                    x.ValueKind == JsonValueKind.Object &&
                    (string.Equals(ReadString(x, "business_wallet_id"), walletId, StringComparison.Ordinal) ||
                     string.Equals(ReadString(x, "wallet_id"), walletId, StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length == 1) item = matches[0];
            else if (matches.Length == 0 && data.GetArrayLength() == 1) item = data[0];
            else throw new InvalidOperationException("Blaaiz returned no unambiguous bank account for the selected wallet.");
        }
        else
        {
            item = data;
        }

        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Blaaiz virtual bank account response was invalid.");

        // Some responses omit the wallet ID after a wallet-scoped request. When
        // supplied, every wallet identifier must agree with the recorded selection.
        foreach (var returnedWallet in new[] { ReadString(item, "business_wallet_id"), ReadString(item, "wallet_id") })
            if (!string.IsNullOrWhiteSpace(returnedWallet) && !string.Equals(returnedWallet, walletId, StringComparison.Ordinal))
                throw new InvalidOperationException("Blaaiz returned a bank account belonging to a different wallet.");

        var id = ReadString(item, "id");
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException(
                "Blaaiz virtual bank account response did not include an account ID.");

        return new VirtualAccountData(
            id,
            ReadString(item, "account_name"),
            ReadString(item, "account_number"),
            ReadString(item, "bank_name"),
            ReadString(item, "bank_code"),
            ReadString(item, "sort_code"),
            ReadString(item, "iban"),
            ReadString(item, "provider"),
            ReadString(item, "account_reference"),
            ReadString(item, "reservation_reference"),
            ReadString(item, "status") ?? "PENDING");
    }

    private static JsonElement GetData(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
            ? data
            : root;

    private static string? ReadString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null
        };
    }

    private sealed record VirtualAccountData(
        string Id,
        string? AccountName,
        string? AccountNumber,
        string? BankName,
        string? BankCode,
        string? SortCode,
        string? Iban,
        string? Provider,
        string? AccountReference,
        string? ReservationReference,
        string Status);
}
