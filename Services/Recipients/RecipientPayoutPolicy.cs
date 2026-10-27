using KorridorX.Models.Enums;
using KorridorX.Models.Recipients;
using KorridorX.Providers.Remittance.Blaaiz;

namespace KorridorX.Services.Recipients;

public static class RecipientPayoutPolicy
{
    public const string LocalValidation = "DetailsChecked_ProviderValidationAtPayout";
    public static PaymentMethod Method(RecipientBankAccount account) =>
        BlaaizCurrencyRequirements.Require(account.CurrencyCode, account.PayoutMethod).PaymentMethod;
    public static Dictionary<string, string?> Validate(RecipientBankAccount account) =>
        BlaaizCurrencyRequirements.Validate(account.CurrencyCode, Method(account), account.CountryCode,
            new Dictionary<string, string?>
            {
                ["providerBankId"] = account.ProviderBankId, ["bankName"] = account.BankName,
                ["accountName"] = account.AccountName, ["accountNumber"] = account.AccountNumber,
                ["iban"] = account.Iban, ["swiftBic"] = account.SwiftBic,
                ["routingNumber"] = account.RoutingNumber, ["sortCode"] = account.SortCode,
                ["recipientType"] = account.RecipientType, ["accountType"] = account.AccountType,
                ["street"] = account.Street, ["city"] = account.City,
                ["state"] = account.State, ["postalCode"] = account.PostalCode
            });
    public static bool IsReady(RecipientBankAccount account)
    {
        if (!account.IsActive || account.IsDeleted) return false;
        if (!BlaaizCurrencyRequirements.IsForeignBank(account.CurrencyCode)) return account.IsVerified;
        try { Validate(account); return true; }
        catch (InvalidOperationException) { return false; }
    }
    public static void Normalize(RecipientBankAccount account)
    {
        var values = Validate(account);
        account.AccountName = values.GetValueOrDefault("accountName") ?? "";
        account.AccountNumber = values.GetValueOrDefault("accountNumber") ?? "";
        if (values.ContainsKey("bankName")) account.BankName = values["bankName"] ?? "";
        account.Iban = values.GetValueOrDefault("iban"); account.SwiftBic = values.GetValueOrDefault("swiftBic");
        account.RoutingNumber = values.GetValueOrDefault("routingNumber"); account.SortCode = values.GetValueOrDefault("sortCode");
        account.RecipientType = values.GetValueOrDefault("recipientType"); account.AccountType = values.GetValueOrDefault("accountType");
        account.Street = values.GetValueOrDefault("street"); account.City = values.GetValueOrDefault("city");
        account.State = values.GetValueOrDefault("state"); account.PostalCode = values.GetValueOrDefault("postalCode");
        account.PayoutMethod = Method(account);
    }
}
