using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;

namespace KorridorX.Services.PaymentChannels;

/// <summary>Executable adapter capabilities, NOT an advertised provider-method enum.</summary>
public static class BlaaizChannelCapabilities
{
    public const string InteracRequest = "interac-request";
    public const string Card = "card";
    public const string VirtualAccount = "virtual-account";
    // The Blaaiz customer is a verified individual (the business's primary
    // owner), while KorridorX assigns settled collections to the business wallet.
    // This is deliberately distinct from a virtual account for type=business.
    public const string BusinessPrincipalVirtualAccount = "business-principal-virtual-account";
    public const string BankPayout = "bank-payout";
    public const string InteracPayout = "interac-payout";

    public static IReadOnlyList<PaymentChannelCapabilityDto> All { get; } = Build();
    public static PaymentChannelCapabilityDto? Find(string code, string asset, PaymentChannelDirection direction) =>
        All.SingleOrDefault(x => x.Code == code && x.AssetCode == asset && x.Direction == direction);

    public static bool IsVirtualBankAccount(string code) =>
        code is VirtualAccount or BusinessPrincipalVirtualAccount;

    public static bool MatchesCountry(string configured, string requested) => configured == "*" || configured == requested;
    public static bool MatchesAmount(decimal minimum, decimal? maximum, decimal amount) =>
        amount >= minimum && (!maximum.HasValue || amount <= maximum.Value);

    private static IReadOnlyList<PaymentChannelCapabilityDto> Build()
    {
        var rows = new List<PaymentChannelCapabilityDto>();
        foreach (var currency in BlaaizCurrencyRequirements.All)
        {
            foreach (var method in currency.PayoutMethods)
            {
                var code = method.PaymentMethod switch
                {
                    PaymentMethod.Ach => "ach-payout", PaymentMethod.Wire => "wire-payout",
                    PaymentMethod.Interac => InteracPayout, _ => BankPayout
                };
                rows.Add(new(code, "BLAAIZ", PaymentChannelDirection.Withdrawal, currency.CurrencyCode,
                    method.PaymentMethod, "TransferPayout", method.DisplayName, true, true, true, null,
                    "Uses the shared Blaaiz currency requirements. Recipient payouts and owner withdrawals retain their respective identity, funding and channel checks."));
            }
            foreach (var method in currency.CollectionMethods)
            {
                var code = method switch
                {
                    PaymentMethod.Card => Card, PaymentMethod.Interac => InteracRequest, _ => VirtualAccount
                };
                var description = method == PaymentMethod.Card
                    ? "Uses the existing native card flow. Business card collection requires an approved web flow."
                    : method == PaymentMethod.Interac
                    ? "Uses the connection's verified default CAD wallet and existing Interac request flow."
                    : "Dedicated customer bank account; never share a pooled treasury account. Foreign virtual accounts require verified individuals, live provider eligibility, and an active receiving rail.";
                rows.Add(new(code, "BLAAIZ", PaymentChannelDirection.Deposit, currency.CurrencyCode, method,
                    method == PaymentMethod.VirtualAccount ? "VirtualAccount" : "Collection",
                    method == PaymentMethod.Card ? "Debit / credit card" : method == PaymentMethod.Interac
                        ? "Interac e-Transfer request" : "Bank transfer (dedicated account)",
                    true, method == PaymentMethod.Interac || method == PaymentMethod.VirtualAccount && currency.CurrencyCode == "NGN",
                    true, null, description));
                if (method == PaymentMethod.VirtualAccount && (currency.CurrencyCode is "USD" or "GBP" or "EUR"))
                {
                    rows.Add(new(BusinessPrincipalVirtualAccount, "BLAAIZ", PaymentChannelDirection.Deposit,
                        currency.CurrencyCode, PaymentMethod.VirtualAccount, "VirtualAccount",
                        "Bank transfer (verified primary holder)",
                        false, true, true, null,
                        "For approved business collections only: the verified primary owner's INDIVIDUAL Blaaiz customer receives an account in that person's legal name. It is mapped to this business wallet. Requires the owner's informed consent, completed personal KYC, business KYB and explicit provider capability confirmation."));
                }
            }
        }
        return rows;
    }
}
