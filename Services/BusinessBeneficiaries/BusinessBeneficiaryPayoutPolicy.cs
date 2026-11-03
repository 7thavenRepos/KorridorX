using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;

namespace KorridorX.Services.BusinessBeneficiaries;

/// <summary>
/// Currency-dependent validation and local destination readiness. Local readiness
/// never means the provider has verified ownership or accepted a payout.
/// </summary>
public static class BusinessBeneficiaryPayoutPolicy
{
    public static PaymentMethod Method(BusinessBeneficiaryBankAccount account) =>
        BlaaizCurrencyRequirements.Require(account.CurrencyCode, account.PayoutMethod).PaymentMethod;

    public static IReadOnlyDictionary<string, string?> Validate(
        BusinessBeneficiaryBankAccount account, bool allowUnverifiedBankDraft = false)
    {
        var method = Method(account);
        var rule = BlaaizCurrencyRequirements.Require(account.CurrencyCode, method);
        if (method == PaymentMethod.Interac)
            throw new InvalidOperationException("Interac uses recipient email, not a bank account.");
        if (!rule.UsesBankDirectory && !string.IsNullOrWhiteSpace(account.ProviderBankId))
            throw new InvalidOperationException("This payout method does not use provider bank IDs.");

        return BlaaizCurrencyRequirements.Validate(
            account.CurrencyCode, method, account.CountryCode,
            new Dictionary<string, string?>
            {
                ["providerBankId"] = account.ProviderBankId,
                ["bankName"] = account.BankName,
                ["accountName"] = account.AccountName,
                ["accountNumber"] = account.AccountNumber,
                ["iban"] = account.Iban,
                ["swiftBic"] = account.SwiftBic,
                ["routingNumber"] = account.RoutingNumber,
                ["sortCode"] = account.SortCode,
                ["recipientType"] = account.RecipientType,
                ["accountType"] = account.AccountType,
                ["street"] = account.Street,
                ["city"] = account.City,
                ["state"] = account.State,
                ["postalCode"] = account.PostalCode,
            },
            allowUnverifiedBankDraft: allowUnverifiedBankDraft && !account.IsVerified);
    }

    /// <summary>
    /// All bank rails require complete local routing details. NGN requires a chosen
    /// provider bank ID and ten-digit account number, but a successful account-name
    /// lookup is optional; provider acceptance/ownership is never inferred.
    /// Any provider-specific destination mapping is checked separately at transfer
    /// creation. Interac is email-based and is not a business bank account.
    /// </summary>
    public static bool IsReady(BusinessBeneficiaryBankAccount account)
    {
        if (account.IsDeleted || !account.IsActive) return false;

        try
        {
            var rule = BlaaizCurrencyRequirements.Require(account.CurrencyCode, account.PayoutMethod);
            if (rule.PaymentMethod == PaymentMethod.Interac) return false;
            Validate(account);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void NormalizeDraft(BusinessBeneficiaryBankAccount account)
    {
        var values = Validate(account, allowUnverifiedBankDraft: true);
        account.PayoutMethod = Method(account);
        account.AccountName = values.GetValueOrDefault("accountName") ?? "";
        account.AccountNumber = values.GetValueOrDefault("accountNumber") ?? "";
        if (values.ContainsKey("bankName")) account.BankName = values["bankName"] ?? "";
        account.Iban = values.GetValueOrDefault("iban");
        account.SwiftBic = values.GetValueOrDefault("swiftBic");
        account.RoutingNumber = values.GetValueOrDefault("routingNumber");
        account.SortCode = values.GetValueOrDefault("sortCode");
        account.RecipientType = values.GetValueOrDefault("recipientType");
        account.AccountType = values.GetValueOrDefault("accountType");
        account.Street = values.GetValueOrDefault("street");
        account.City = values.GetValueOrDefault("city");
        account.State = values.GetValueOrDefault("state");
        account.PostalCode = values.GetValueOrDefault("postalCode");
    }
}
