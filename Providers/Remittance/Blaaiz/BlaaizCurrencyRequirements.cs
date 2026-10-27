using System.Text.RegularExpressions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Providers.Remittance.Blaaiz;

public sealed record BlaaizFieldRequirement(string Key, string Label, bool Required = true,
    int MaxLength = 100, string? Pattern = null, IReadOnlyList<string>? Choices = null,
    string Input = "text");
public sealed record BlaaizPayoutMethodRequirement(PaymentMethod PaymentMethod, string Code,
    string DisplayName, bool UsesBankDirectory, bool RequiresProviderAccountVerification,
    IReadOnlyList<BlaaizFieldRequirement> Fields);
public sealed record BlaaizCurrencyRequirement(string CurrencyCode, string CountryCode,
    IReadOnlyList<PaymentMethod> CollectionMethods, IReadOnlyList<BlaaizPayoutMethodRequirement> PayoutMethods,
    bool TransferCollectionEnabled = false)
{
    public IReadOnlyList<string> BankCountryCodes => CountryCode == "*"
        ? BlaaizCurrencyRequirements.EurBankCountryCodes : new[] { CountryCode };
}

/// <summary>
/// One executable Blaaiz fiat contract for recipient forms, validation, provider
/// payloads, channels and owner withdrawals. Countries/assets/channels still
/// require administrator configuration; this catalog never enables them.
/// Source: docs.blaaiz.com/docs/payments/payout/create-a-payout (2026-10-27).
/// A local format check does not establish account ownership, CoP or reachability.
/// </summary>
public static class BlaaizCurrencyRequirements
{
    private static readonly BlaaizFieldRequirement Name = new("accountName", "Account holder name", MaxLength: 200);
    private static readonly BlaaizFieldRequirement Bank = new("bankName", "Bank name", MaxLength: 150);
    private static readonly BlaaizFieldRequirement[] Address =
    [
        new("street", "Street address", MaxLength: 150),
        new("city", "City", MaxLength: 100),
        new("postalCode", "Postal code", MaxLength: 30)
    ];
    private const string SwiftPattern = "^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$";
    // EPC geographical scope v8.0 (2025-12-24), with GI's separate IBAN country.
    // Scope alone does not prove a particular bank/IBAN is reachable.
    public static IReadOnlyList<string> EurBankCountryCodes { get; } = Array.AsReadOnly(new[]
    {
        "AD", "AL", "AT", "BE", "BG", "CH", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR",
        "GB", "GI", "GR", "HR", "HU", "IE", "IS", "IT", "LI", "LT", "LU", "LV", "MC", "MD",
        "ME", "MK", "MT", "NL", "NO", "PL", "PT", "RO", "RS", "SE", "SI", "SK", "SM", "VA"
    });
    public static IReadOnlyList<BlaaizCurrencyRequirement> All { get; } = Build().AsReadOnly();

    private static List<BlaaizCurrencyRequirement> Build()
    {
        var us = new BlaaizFieldRequirement[]
        {
            Name, Bank, new("accountNumber", "Account number", MaxLength: 17, Pattern: "^[0-9]{4,17}$", Input: "digits"),
            new("routingNumber", "Routing number", MaxLength: 9, Pattern: "^[0-9]{9}$", Input: "digits"),
            new("accountType", "Account type", MaxLength: 20, Choices: new[] { "checking", "savings" }),
            new("recipientType", "Recipient type", MaxLength: 20, Choices: new[] { "individual", "business" }),
            new("state", "State", MaxLength: 100)
        }.Concat(Address).ToArray();
        return
        [
            new("NGN", "NG", new[] { PaymentMethod.Card, PaymentMethod.VirtualAccount },
            [new(PaymentMethod.BankTransfer, "bank_transfer", "Bank transfer", true, true,
                [new("providerBankId", "Bank", MaxLength: 150, Input: "bank"),
                 new("accountNumber", "Account number", MaxLength: 10, Pattern: "^[0-9]{10}$", Input: "digits"),
                 new("accountName", "Account name (optional)", false, 200)])]),
            new("CAD", "CA", new[] { PaymentMethod.Interac },
            [new(PaymentMethod.Interac, "interac", "Interac e-Transfer", false, false,
                [new("email", "Interac email", MaxLength: 254, Input: "email"),
                 new("firstName", "First name", MaxLength: 100), new("lastName", "Last name", MaxLength: 100)])], true),
            new("USD", "US", new[] { PaymentMethod.Card, PaymentMethod.VirtualAccount },
            [new(PaymentMethod.Ach, "ach", "USD ACH", false, false, us),
             new(PaymentMethod.Wire, "wire", "USD Wire", false, false,
                 us.Append(new BlaaizFieldRequirement("swiftBic", "SWIFT code", MaxLength: 11, Pattern: SwiftPattern)).ToArray())], true),
            new("GBP", "GB", new[] { PaymentMethod.VirtualAccount },
            [new(PaymentMethod.BankTransfer, "bank_transfer", "GBP bank transfer", false, false,
                new BlaaizFieldRequirement[] { Name,
                    new("bankName", "Bank name (optional)", false, 150),
                    new("accountNumber", "Account number", MaxLength: 8, Pattern: "^[0-9]{8}$", Input: "digits"),
                    new("sortCode", "Sort code", MaxLength: 6, Pattern: "^[0-9]{6}$", Input: "digits") }.Concat(Address).ToArray())]),
            // '*' means a real, configured EUR bank country, never a fabricated EU country.
            new("EUR", "*", new[] { PaymentMethod.VirtualAccount },
            [new(PaymentMethod.BankTransfer, "bank_transfer", "EUR SEPA bank transfer", false, false,
                new BlaaizFieldRequirement[] { Name,
                    new("bankName", "Bank name (optional)", false, 150),
                    new("iban", "IBAN", MaxLength: 34, Pattern: "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"),
                    new("swiftBic", "BIC", MaxLength: 11, Pattern: SwiftPattern) }.Concat(Address).ToArray())])
        ];
    }

    public static BlaaizCurrencyRequirement? Find(string? currency) =>
        All.SingleOrDefault(x => x.CurrencyCode == currency?.Trim().ToUpperInvariant());
    public static bool SupportsCountry(string? currency, string? country)
    {
        var requirement = Find(currency);
        var code = country?.Trim().ToUpperInvariant() ?? "";
        return requirement is not null && requirement.BankCountryCodes.Contains(code);
    }
    public static BlaaizPayoutMethodRequirement Require(string currency, PaymentMethod? method = null)
    {
        var entry = Find(currency) ?? throw new InvalidOperationException("This currency has no implemented Blaaiz fiat payout method.");
        return method.HasValue
            ? entry.PayoutMethods.SingleOrDefault(x => x.PaymentMethod == method.Value)
                ?? throw new InvalidOperationException("The payout method is not supported for this currency.")
            : entry.PayoutMethods[0];
    }
    public static bool IsForeignBank(string currency) => Find(currency) is { } row &&
        row.PayoutMethods.All(x => !x.UsesBankDirectory && x.PaymentMethod != PaymentMethod.Interac);

    public static Dictionary<string, string?> Validate(string currency, PaymentMethod method, string country,
        IReadOnlyDictionary<string, string?> supplied, bool checkFormat = true)
    {
        var rule = Require(currency, method);
        if (!SupportsCountry(currency, country))
            throw new InvalidOperationException("The recipient country does not support this payout currency.");
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in rule.Fields)
        {
            supplied.TryGetValue(field.Key, out var raw);
            var value = raw?.Trim() ?? "";
            if (value.Contains('\r') || value.Contains('\n'))
                throw new InvalidOperationException(field.Label + " is invalid or too long.");
            if (field.Key is "iban" or "swiftBic") value = value.Replace(" ", "").ToUpperInvariant();
            if (field.Key == "sortCode") value = value.Replace("-", "").Replace(" ", "");
            if (field.Key is "accountType" or "recipientType" or "email") value = value.ToLowerInvariant();
            if (value.Length > field.MaxLength)
                throw new InvalidOperationException(field.Label + " is too long.");
            if (field.Required && value.Length == 0)
                throw new InvalidOperationException(field.Label + " is required.");
            if (value.Length > 0 && (field.Choices is not null && !field.Choices.Contains(value) ||
                checkFormat && field.Pattern is not null && !Regex.IsMatch(value, field.Pattern, RegexOptions.CultureInvariant)))
                throw new InvalidOperationException("Enter a valid " + field.Label.ToLowerInvariant() + ".");
            result[field.Key] = value.Length == 0 ? null : value;
        }
        if (checkFormat && result.TryGetValue("iban", out var iban) && iban is not null &&
            (!ValidIban(iban) || !iban.StartsWith(country.Trim().ToUpperInvariant(), StringComparison.Ordinal)))
            throw new InvalidOperationException("Enter a valid IBAN matching the recipient bank country.");
        if (result.TryGetValue("email", out var email) &&
            (email is null || !System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
            throw new InvalidOperationException("Enter a valid single Interac email address.");
        return result;
    }

    public static void ApplyPayoutFields(RemittancePayoutRequest request, BlaaizPayoutRequest target)
    {
        var currency = request.DestinationCurrencyCode.Trim().ToUpperInvariant();
        var country = request.RecipientCountry ?? Find(currency)?.CountryCode ?? "";
        var values = Validate(currency, request.PaymentMethod, country, new Dictionary<string, string?>
        {
            ["providerBankId"] = request.BankId, ["accountName"] = request.AccountName,
            ["bankName"] = request.BankName, ["accountNumber"] = request.AccountNumber,
            ["routingNumber"] = request.RoutingNumber, ["sortCode"] = request.SortCode,
            ["iban"] = request.Iban, ["swiftBic"] = currency == "EUR" ? request.BicCode ?? request.SwiftBic : request.SwiftBic,
            ["accountType"] = request.AccountType, ["recipientType"] = request.RecipientType,
            ["state"] = request.RecipientState, ["street"] = request.RecipientStreet,
            ["city"] = request.RecipientCity, ["postalCode"] = request.RecipientPostalCode,
            ["email"] = request.RecipientEmail, ["firstName"] = request.RecipientFirstName,
            ["lastName"] = request.RecipientLastName
        }, checkFormat: false);
        string? Value(string key) => values.GetValueOrDefault(key);
        target.BankId = Value("providerBankId"); target.AccountName = Value("accountName");
        target.BankName = Value("bankName"); target.AccountNumber = Value("accountNumber");
        target.RoutingNumber = Value("routingNumber"); target.SortCode = Value("sortCode");
        target.Iban = Value("iban");
        target.BicCode = currency == "EUR" ? Value("swiftBic") : null;
        target.SwiftCode = currency == "USD" ? Value("swiftBic") : null;
        target.AccountType = Value("accountType"); target.RecipientType = Value("recipientType");
        target.State = Value("state"); target.Street = Value("street");
        target.City = Value("city"); target.ZipCode = Value("postalCode");
        target.Country = IsForeignBank(currency) ? country.Trim().ToUpperInvariant() : null;
        target.Email = Value("email"); target.InteracFirstName = Value("firstName");
        target.InteracLastName = Value("lastName");
    }

    private static bool ValidIban(string value)
    {
        if (value.Length < 15 || value.Length > 34) return false;
        var remainder = 0;
        foreach (var c in value[4..] + value[..4])
        {
            if (c is >= '0' and <= '9') remainder = (remainder * 10 + c - '0') % 97;
            else if (c is >= 'A' and <= 'Z') remainder = (remainder * 100 + c - 'A' + 10) % 97;
            else return false;
        }
        return remainder == 1;
    }
}
