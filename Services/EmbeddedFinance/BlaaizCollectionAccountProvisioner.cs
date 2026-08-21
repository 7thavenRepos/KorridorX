using System.Text.Json;
using KorridorX.Data;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class BlaaizCollectionAccountProvisioner : ICollectionAccountProvisioner
{
    private readonly AppDbContext _db;
    private readonly IBlaaizApiClient _blaaiz;

    public BlaaizCollectionAccountProvisioner(AppDbContext db, IBlaaizApiClient blaaiz)
    {
        _db = db;
        _blaaiz = blaaiz;
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

        var walletId = await ResolveCadWalletIdAsync(ct);
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
                rawResponse = raw
            }));
    }

    private async Task<string> ResolveCadWalletIdAsync(CancellationToken ct)
    {
        var wallets = await _blaaiz.ListWalletsAsync(ct);
        using var doc = JsonDocument.Parse(wallets.RawResponseJson);
        var data = GetData(doc.RootElement);

        if (data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Blaaiz wallet response did not contain a wallet list.");

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var currency =
                ReadString(item, "currency_code") ??
                ReadString(item, "currencyCode") ??
                ReadCurrencyObjectCode(item, "currency") ??
                ReadString(item, "currency");

            if (!string.Equals(currency, "CAD", StringComparison.OrdinalIgnoreCase))
                continue;

            var id =
                ReadString(item, "id") ??
                ReadString(item, "wallet_id") ??
                ReadString(item, "walletId");

            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        throw new InvalidOperationException("Blaaiz CAD business wallet was not found.");
    }

    private static VirtualAccountData ParseVirtualAccount(string raw, string walletId)
    {
        using var doc = JsonDocument.Parse(raw);
        var data = GetData(doc.RootElement);
        JsonElement item;

        if (data.ValueKind == JsonValueKind.Array)
        {
            var match = data.EnumerateArray()
                .FirstOrDefault(x =>
                    x.ValueKind == JsonValueKind.Object &&
                    (string.Equals(ReadString(x, "business_wallet_id"), walletId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ReadString(x, "wallet_id"), walletId, StringComparison.OrdinalIgnoreCase)));

            if (match.ValueKind == JsonValueKind.Undefined)
                match = data.EnumerateArray().FirstOrDefault();

            item = match;
        }
        else
        {
            item = data;
        }

        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Blaaiz virtual bank account response was invalid.");

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

    private static string? ReadCurrencyObjectCode(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
            return null;

        return ReadString(property, "code") ??
               ReadString(property, "currency_code") ??
               ReadString(property, "currencyCode");
    }

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
