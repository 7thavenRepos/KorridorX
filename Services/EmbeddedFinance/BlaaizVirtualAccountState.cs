using System.Text.Json;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;

namespace KorridorX.Services.EmbeddedFinance;

// Provider account lifecycle messages carry no payment and must never post money.
public sealed record BlaaizVirtualAccountState(
    string Id, string? CustomerId, string? WalletId, string? Currency,
    ProviderAccountMappingStatus Status, string? AccountNumber, string? AccountName,
    string? BankName, string? BankCode, string? SortCode, string? Iban,
    string? Reference, string? FailureReason)
{
    public static BlaaizVirtualAccountState ParseResponse(
        string raw, string walletId, string? customerId = null, string? assetCode = null)
    {
        using var document = JsonDocument.Parse(raw);
        var item = Data(document.RootElement);
        if (item.ValueKind == JsonValueKind.Array)
        {
            var matches = item.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object &&
                Matches(x, walletId, customerId, assetCode)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Blaaiz returned no unambiguous bank account for the selected wallet and customer.");
            item = matches[0];
        }
        if (item.ValueKind != JsonValueKind.Object || !Matches(item, walletId, customerId, assetCode))
            throw new InvalidOperationException("Blaaiz returned a bank account for a different wallet, customer or currency.");
        return Parse(item, null, assetCode);
    }

    public static BlaaizVirtualAccountState ParseWebhook(string raw, string eventType)
    {
        using var document = JsonDocument.Parse(raw);
        var item = Data(document.RootElement);
        var state = Parse(item, eventType, null);
        if (string.IsNullOrWhiteSpace(state.CustomerId) || string.IsNullOrWhiteSpace(state.WalletId) ||
            string.IsNullOrWhiteSpace(state.Currency))
            throw new InvalidOperationException("Virtual-account webhook requires customer_id, wallet_id and currency.");
        return state;
    }

    private static BlaaizVirtualAccountState Parse(JsonElement item, string? eventType, string? assetCode)
    {
        var id = Read(item, "virtual_account_id") ?? Read(item, "id");
        if (string.IsNullOrWhiteSpace(id) || id.Length > 150)
            throw new InvalidOperationException("Blaaiz virtual account response requires an account ID of at most 150 characters.");
        var status = Read(item, "status")?.ToUpperInvariant();
        var expected = eventType?.ToLowerInvariant() switch
        {
            null => null,
            "virtual_account.created" => "PENDING",
            "virtual_account.ready" => "SUCCESSFUL",
            "virtual_account.failed" => "FAILED",
            "virtual_account.rejected" => "REJECTED",
            _ => throw new InvalidOperationException("Unsupported virtual-account event.")
        };
        if (expected is not null && status != expected)
            throw new InvalidOperationException("Virtual-account event and status disagree.");
        var mapped = status switch
        {
            "SUCCESSFUL" => ProviderAccountMappingStatus.Active,
            "PENDING" or null => ProviderAccountMappingStatus.Pending,
            "FAILED" or "REJECTED" => ProviderAccountMappingStatus.Failed,
            _ => throw new InvalidOperationException("Unrecognized virtual-account status.")
        };
        var currency = Read(item, "currency")?.ToUpperInvariant() ?? assetCode?.ToUpperInvariant();
        var number = Read(item, "account_number");
        var iban = Read(item, "iban");
        var sortCode = Read(item, "sort_code");
        if (mapped == ProviderAccountMappingStatus.Active &&
            (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(iban)))
            throw new InvalidOperationException("Ready virtual account is missing bank account details.");
        if (mapped == ProviderAccountMappingStatus.Active && currency == "EUR" && string.IsNullOrWhiteSpace(iban))
            throw new InvalidOperationException("Ready EUR account is missing its IBAN.");
        if (mapped == ProviderAccountMappingStatus.Active && currency == "GBP" &&
            (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(sortCode)))
            throw new InvalidOperationException("Ready GBP account requires an account number and sort code.");
        return new(id, Read(item, "customer_id"), Read(item, "wallet_id") ?? Read(item, "business_wallet_id"),
            currency, mapped, Bounded(number, 150), Bounded(Read(item, "account_name"), 200),
            Bounded(Read(item, "bank_name"), 200), Bounded(Read(item, "bank_code"), 50),
            Bounded(sortCode, 50), Bounded(iban, 50),
            Bounded(Read(item, "account_reference") ?? Read(item, "reservation_reference"), 150),
            Bounded(Read(item, "user_reason") ?? Read(item, "rejection_reason"), 1000));
    }

    public void Apply(ProviderAccountMapping mapping, string selectedWalletId)
    {
        if (!string.IsNullOrWhiteSpace(mapping.ProviderAccountId) && mapping.ProviderAccountId != Id)
            throw new InvalidOperationException("Virtual-account ID does not match the recorded account.");
        if (!string.IsNullOrWhiteSpace(mapping.ProviderCustomerId) && CustomerId is not null && mapping.ProviderCustomerId != CustomerId)
            throw new InvalidOperationException("Virtual-account customer does not match the recorded customer.");
        if (WalletId is not null && WalletId != selectedWalletId)
            throw new InvalidOperationException("Virtual-account wallet does not match the durable wallet selection.");
        if (Currency is not null && !string.Equals(Currency, mapping.CollectionAccount.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Virtual-account currency does not match the collection account.");

        // Terminal messages cannot undo a ready account, a rejection, or an admin disable.
        // A deliberate provisioning retry moves Failed back to Pending first.
        if (mapping.Status != ProviderAccountMappingStatus.Pending) return;
        mapping.ProviderAccountId = Id;
        mapping.ProviderCustomerId = CustomerId ?? mapping.ProviderCustomerId;
        mapping.ProviderReference = Reference ?? mapping.ProviderReference;
        mapping.AccountNumber = AccountNumber;
        mapping.AccountName = AccountName;
        mapping.BankName = BankName;
        mapping.Status = Status;
        mapping.FailureReason = Status == ProviderAccountMappingStatus.Failed
            ? FailureReason ?? "Blaaiz could not provision this account." : null;
        mapping.MetadataJson = Metadata(selectedWalletId);
        mapping.LastUpdatedAt = DateTime.UtcNow;
        var account = mapping.CollectionAccount;
        if (Status == ProviderAccountMappingStatus.Active && account.Status == CollectionAccountStatus.Pending &&
            account.BusinessCustomer.Status == BusinessCustomerStatus.Active)
        {
            account.Status = CollectionAccountStatus.Active;
            account.LastUpdatedAt = mapping.LastUpdatedAt;
        }
    }

    public string Metadata(string walletId) => JsonSerializer.Serialize(new
    {
        BankCode, SortCode, Iban, providerWalletId = walletId, providerAccountStatus = Status.ToString()
    });

    public static VirtualAccountBankDetails? BankDetails(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            return new(Read(doc.RootElement, "BankCode"), Read(doc.RootElement, "SortCode"), Read(doc.RootElement, "Iban"));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    private static bool Matches(JsonElement item, string walletId, string? customerId, string? assetCode) =>
        new[] { Read(item, "wallet_id"), Read(item, "business_wallet_id") }
            .All(x => x is null || x == walletId) &&
        (customerId is null || Read(item, "customer_id") is not { } customer || customer == customerId) &&
        (assetCode is null || Read(item, "currency") is not { } currency || string.Equals(currency, assetCode, StringComparison.OrdinalIgnoreCase));

    private static JsonElement Data(JsonElement root) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) ? data : root;
    private static string? Read(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Blaaiz bank account field '{name}' must be text.");
        return value.GetString()?.Trim() is { Length: > 0 } text ? text : null;
    }
    private static string? Bounded(string? value, int max) => value?.Length > max
        ? throw new InvalidOperationException("Blaaiz bank account field exceeds its supported length.") : value;
}

public sealed record VirtualAccountBankDetails(string? BankCode, string? SortCode, string? Iban);
