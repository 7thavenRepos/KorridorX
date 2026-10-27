using KorridorX.Providers.Remittance.Blaaiz;
using System.Text.RegularExpressions;
using KorridorX.Models.Enums;

namespace KorridorX.Services.WalletWithdrawals;

public static class WalletWithdrawalSetupPolicy
{
    // The payout currency always equals the source wallet currency. EUR uses
    // the actual beneficiary bank's SEPA country, never a fabricated "EU" code.
    public static string DestinationCountry(string currency, PaymentMethod method, string? selectedCountry = null)
    {
        BlaaizCurrencyRequirements.Require(currency, method);
        var contract = BlaaizCurrencyRequirements.Find(currency)!;
        var country = contract.CountryCode == "*" ? selectedCountry : contract.CountryCode;
        if (country is not { Length: 2 } || !country.All(c => c is >= 'A' and <= 'Z') ||
            !BlaaizCurrencyRequirements.SupportsCountry(currency, country))
            throw new InvalidOperationException("Choose a supported recipient bank country.");
        return country!;
    }

    public static string NormalizeNgnAccountNumber(string? raw)
    {
        if (raw is not null && (raw.Contains('\r') || raw.Contains('\n')))
            throw new InvalidOperationException("Enter a 10-digit Nigerian bank account number.");
        var value = raw?.Trim() ?? "";
        if (!Regex.IsMatch(value, "^[0-9]{10}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Enter a 10-digit Nigerian bank account number.");
        return value;
    }

    public static string NormalizeVerifiedEmail(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? "";
        if (value.Length == 0 || value.Length > 254 || value.Contains('\n') || value.Contains('\r') ||
            !System.Net.Mail.MailAddress.TryCreate(value, out var parsed) ||
            !string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Enter a valid single Interac email address.");
        return value;
    }

    // Bank provider name enquiry is not proof of ownership unless it matches the
    // verified customer/business identity. Extra middle names and legal suffixes
    // are permitted, but different significant name tokens fail closed.
    public static bool IsOwnAccount(string? expectedLegalName, string? providerVerifiedName, bool business = false)
    {
        var expected = Tokens(expectedLegalName);
        var actual = Tokens(providerVerifiedName);
        if (business && expected.Length == 1 && expected[0].Length >= 5 && actual.Length == 1)
            return expected[0] == actual[0];
        return expected.Length >= 2 && actual.Length >= 2 &&
            expected.All(x => actual.Contains(x));
    }

    private static string[] Tokens(string? text)
    {
        var normalized = Regex.Replace((text ?? "").ToUpperInvariant(), "[^A-Z0-9]+", " ").Trim();
        var ignore = new HashSet<string>(StringComparer.Ordinal)
        { "MR", "MRS", "MISS", "MS", "DR", "THE", "LTD", "LIMITED", "INC", "LLC", "PLC", "CO", "COMPANY" };
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 1 && !ignore.Contains(x)).Distinct().ToArray();
    }

    public static string Mask(string accountNumber) => "•••• " + accountNumber[^4..];
}
