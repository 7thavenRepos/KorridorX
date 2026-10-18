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
    public const string BankPayout = "bank-payout";
    public const string InteracPayout = "interac-payout";

    public static IReadOnlyList<PaymentChannelCapabilityDto> All { get; } = Build();
    public static PaymentChannelCapabilityDto? Find(string code, string asset, PaymentChannelDirection direction) =>
        All.SingleOrDefault(x => x.Code == code && x.AssetCode == asset && x.Direction == direction);

    public static bool MatchesCountry(string configured, string requested) => configured == "*" || configured == requested;
    public static bool MatchesAmount(decimal minimum, decimal? maximum, decimal amount) =>
        amount >= minimum && (!maximum.HasValue || amount <= maximum.Value);

    private static IReadOnlyList<PaymentChannelCapabilityDto> Build()
    {
        var rows = new List<PaymentChannelCapabilityDto>
        {
            new(InteracRequest, "BLAAIZ", PaymentChannelDirection.Deposit, "CAD", PaymentMethod.Interac,
                "Collection", "Interac e-Transfer request", true, true, true, null,
                "The existing Interac request endpoint uses the connection's CAD wallet. Select its verified default CAD collection wallet; this is not a separately routed multi-wallet API."),
            new(BankPayout, "BLAAIZ", PaymentChannelDirection.Withdrawal, "NGN", PaymentMethod.BankTransfer,
                "TransferPayout", "Bank transfer", true, true, true, null,
                "Uses the existing verified NGN recipient and remittance payout flow. The funding wallet must match the transfer's source asset."),
            new(InteracPayout, "BLAAIZ", PaymentChannelDirection.Withdrawal, "CAD", PaymentMethod.Interac,
                "TransferPayout", "Interac e-Transfer", true, true, true, null,
                "Uses the existing CAD recipient email and remittance payout flow. Not a new standalone withdrawal endpoint.")
        };
        foreach (var asset in new[] { "NGN", "USD" })
            rows.Add(new(Card, "BLAAIZ", PaymentChannelDirection.Deposit, asset, PaymentMethod.Card,
                "Collection", "Debit / credit card", true, false, true, null,
                "Uses the existing native card flow, not a hosted-only checkout. Business card collection is unavailable until an approved hosted/tokenized web flow is integrated."));
        foreach (var asset in new[] { "NGN", "USD", "EUR", "GBP" })
            rows.Add(new(VirtualAccount, "BLAAIZ", PaymentChannelDirection.Deposit, asset, PaymentMethod.VirtualAccount,
                "VirtualAccount", asset == "EUR" ? "Bank transfer (SEPA account)" : asset == "USD" ? "Bank transfer (USD account)" : "Bank transfer (dedicated account)",
                true, asset == "NGN", true, null,
                asset == "NGN" ? "Requires a verified customer and the provider's virtual-account feature/scope. Never share a pooled treasury account as a customer deposit account."
                : "Blaaiz currently permits verified individual customers only. GBP/EUR creation is on hold on api-dev; USD creation works there, but USD/EUR/GBP deposits do not settle in the provider development environment."));
        foreach (var item in new[] { ("USD", "ach-payout", PaymentMethod.Ach), ("USD", "wire-payout", PaymentMethod.Wire),
                     ("EUR", BankPayout, PaymentMethod.BankTransfer), ("GBP", BankPayout, PaymentMethod.BankTransfer) })
            rows.Add(new(item.Item2, "BLAAIZ", PaymentChannelDirection.Withdrawal, item.Item1, item.Item3,
                "TransferPayout", item.Item3.ToString(), true, true, false,
                "Required recipient fields, eligibility checks and payout execution are not connected in this KorridorX release.",
                "Provider documentation is not proof of an implemented KorridorX flow. This capability cannot be activated."));
        return rows;
    }
}
