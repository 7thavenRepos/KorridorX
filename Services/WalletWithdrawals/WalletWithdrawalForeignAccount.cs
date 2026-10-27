using KorridorX.Providers.Remittance.Blaaiz;
using System.Text.Json;
using System.Text.RegularExpressions;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;

namespace KorridorX.Services.WalletWithdrawals;

// Foreign bank details are stored inside the existing authenticated encrypted
// destination/snapshot field. This preserves the immutable payout snapshot and
// requires no second repository, schema or plaintext bank-detail columns.
public sealed class WalletWithdrawalForeignAccount
{
    private const string Prefix = "KXFOREIGN1:";
    public string CurrencyCode { get; set; } = "";
    public PaymentMethod Method { get; set; }
    public string RecipientType { get; set; } = "individual";
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string RoutingNumber { get; set; } = "";
    public string SwiftCode { get; set; } = "";
    public string SortCode { get; set; } = "";
    public string Iban { get; set; } = "";
    public string BicCode { get; set; } = "";
    public string Country { get; set; } = "";
    public string State { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";

    public string Identifier => CurrencyCode == "EUR" ? Iban : AccountNumber;
    public static bool Supports(string currency, PaymentMethod method) =>
        BlaaizCurrencyRequirements.IsForeignBank(currency) &&
        BlaaizCurrencyRequirements.Find(currency)!.PayoutMethods.Any(x => x.PaymentMethod == method);

    public static WalletWithdrawalForeignAccount Create(string currency, PaymentMethod method,
        SaveWalletWithdrawalDestinationRequestDto request, string ownerName, bool business)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Supports(currency, method)) throw new InvalidOperationException("Unsupported foreign-currency payout rail.");
        var name = Required(request.AccountName, 250, "Account name");
        if (!WalletWithdrawalSetupPolicy.IsOwnAccount(ownerName, name, business))
            throw new InvalidOperationException("The account name must match the verified wallet owner. Use Send for another beneficiary.");
        var result = new WalletWithdrawalForeignAccount
        {
            CurrencyCode = currency.ToUpperInvariant(), Method = method,
            RecipientType = business ? "business" : "individual", AccountName = name,
            AccountNumber = Optional(request.AccountNumber, 34),
            BankName = Optional(request.BankName, 150),
            AccountType = Optional(request.AccountType, 20).ToLowerInvariant(),
            RoutingNumber = Optional(request.RoutingNumber, 20),
            SwiftCode = Optional(request.SwiftCode, 20).ToUpperInvariant(),
            SortCode = Optional(request.SortCode, 20).Replace("-", "").Replace(" ", ""),
            Iban = Optional(request.Iban, 50).Replace(" ", "").ToUpperInvariant(),
            BicCode = Optional(request.BicCode, 20).ToUpperInvariant(),
            Country = Required(request.DestinationCountryCode, 2, "Destination country").ToUpperInvariant(),
            State = Optional(request.State, 100),
            Street = Required(request.Street, 150, "Recipient address"),
            City = Required(request.City, 100, "Recipient city"),
            ZipCode = Required(request.ZipCode, 30, "Recipient postal code")
        };
        result.Validate();
        return result;
    }

    public string ToProtectedPlaintext()
    {
        Validate();
        var value = Prefix + JsonSerializer.Serialize(this);
        if (value.Length > 1450)
            throw new InvalidOperationException("Bank details exceed the supported encrypted storage size.");
        return value;
    }

    public static WalletWithdrawalForeignAccount Parse(string? plaintext, string currency, PaymentMethod method)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || !plaintext.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The saved bank payout details cannot be decoded; no payout was submitted.");
        WalletWithdrawalForeignAccount row;
        try { row = JsonSerializer.Deserialize<WalletWithdrawalForeignAccount>(plaintext[Prefix.Length..])
            ?? throw new FormatException(); }
        catch (JsonException) { throw new InvalidOperationException("The saved bank payout details are invalid; no payout was submitted."); }
        catch (FormatException) { throw new InvalidOperationException("The saved bank payout details are missing; no payout was submitted."); }
        if (!string.Equals(row.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) || row.Method != method)
            throw new InvalidOperationException("Stored destination differs from its withdrawal currency or method.");
        row.Validate();
        return row;
    }

    private void Validate()
    {
        if (!Supports(CurrencyCode, Method))
            throw new InvalidOperationException("Unsupported foreign-currency payout rail.");
        BlaaizCurrencyRequirements.Validate(CurrencyCode, Method, Country,
            new Dictionary<string, string?>
            {
                ["recipientType"] = RecipientType, ["accountName"] = AccountName,
                ["accountNumber"] = AccountNumber, ["bankName"] = BankName, ["accountType"] = AccountType,
                ["routingNumber"] = RoutingNumber, ["swiftBic"] = CurrencyCode == "EUR" ? BicCode : SwiftCode,
                ["sortCode"] = SortCode, ["iban"] = Iban, ["state"] = State,
                ["street"] = Street, ["city"] = City, ["postalCode"] = ZipCode
            });
    }

    private static string Required(string? raw, int max, string label)
    {
        var v = raw?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(v) || v.Length > max || v.Contains('\n') || v.Contains('\r'))
            throw new InvalidOperationException(label + " is required and must be valid.");
        return v;
    }
    private static string Optional(string? raw, int max)
    {
        var v = raw?.Trim() ?? "";
        if (v.Length > max || v.Contains('\n') || v.Contains('\r'))
            throw new InvalidOperationException("Bank payout field is invalid or too long.");
        return v;
    }
}
