using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalSettlementPolicyTests
{
    private const string ProviderId = "provider-payout-1";

    [Theory]
    [InlineData("SUCCESSFUL", WalletWithdrawalSettlementDecision.Capture)]
    [InlineData("COMPLETED", WalletWithdrawalSettlementDecision.Capture)]
    [InlineData("FAILED", WalletWithdrawalSettlementDecision.Release)]
    [InlineData("REJECTED", WalletWithdrawalSettlementDecision.Release)]
    [InlineData("PROCESSING", WalletWithdrawalSettlementDecision.AwaitProvider)]
    [InlineData("PENDING", WalletWithdrawalSettlementDecision.AwaitProvider)]
    [InlineData("REVERSED", WalletWithdrawalSettlementDecision.RequireReconciliation)]
    [InlineData("unknown", WalletWithdrawalSettlementDecision.RequireReconciliation)]
    public void Verified_payout_status_never_implies_unconfirmed_wallet_mutation(
        string status, WalletWithdrawalSettlementDecision expected) =>
        Assert.Equal(expected, WalletWithdrawalSettlementPolicy.Classify(Withdrawal(), ProviderId, Result(status)));

    [Theory]
    [InlineData("merchant")]
    [InlineData("provider")]
    [InlineData("currency")]
    [InlineData("type")]
    [InlineData("amount")]
    [InlineData("fee")]
    [InlineData("net")]
    [InlineData("feeCurrency")]
    [InlineData("missingNet")]
    [InlineData("missingFee")]
    public void Unmatched_evidence_must_not_capture_or_release(string variant)
    {
        var verified = Result("SUCCESSFUL");
        var pinnedId = ProviderId;
        verified = variant switch
        {
            "merchant" => verified with { MerchantReference = "wrong" },
            "provider" => verified with { ProviderTransactionId = "another" },
            "currency" => verified with { CurrencyCode = "CAD" },
            "type" => verified with { TransactionType = "collection" },
            "amount" => verified with { Amount = 96.49m },
            "fee" => verified with { ProviderFeeAmount = 3.49m },
            "net" => verified with { AmountWithoutFee = 96.49m },
            "feeCurrency" => verified with { ProviderFeeCurrencyCode = "USD" },
            "missingNet" => verified with { AmountWithoutFee = null },
            _ => verified with { ProviderFeeAmount = null }
        };
        Assert.Equal(WalletWithdrawalSettlementDecision.RequireReconciliation,
            WalletWithdrawalSettlementPolicy.Classify(Withdrawal(), pinnedId, verified));
    }

    [Fact]
    public void Blaaiz_json_has_exclusive_amount_modes_and_merchant_reference()
    {
        var withdrawal = Withdrawal();
        var request = new BlaaizPayoutRequest
        {
            WalletId = "wallet", CustomerId = "customer", Method = "interac",
            FromCurrencyId = "CAD", ToCurrencyId = "CAD"
        };
        BlaaizPayoutAmountPolicy.Apply(PayoutRequest(100m, withdrawal.Reference), request);
        var json = JsonSerializer.Serialize(request);
        Assert.Contains("\"from_amount\":100", json);
        Assert.DoesNotContain("\"to_amount\"", json);
        Assert.Contains("\"merchant_reference\":", json);
    }

    [Fact]
    public void Existing_remittance_to_amount_mode_is_not_changed()
    {
        var payload = new BlaaizPayoutRequest();
        BlaaizPayoutAmountPolicy.Apply(PayoutRequest(null, null), payload);
        Assert.Null(payload.FromAmount);
        Assert.Equal(96.5m, payload.ToAmount);
        Assert.DoesNotContain("\"from_amount\"", JsonSerializer.Serialize(payload));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Inclusive_mode_fails_if_wallet_debit_cannot_cover_recipient(decimal invalidAmount) =>
        Assert.Throws<InvalidOperationException>(() => BlaaizPayoutAmountPolicy.Apply(
            PayoutRequest(invalidAmount, Withdrawal().Reference), new BlaaizPayoutRequest()));

    [Fact]
    public void Inclusive_mode_requires_unique_merchant_reference() =>
        Assert.Throws<InvalidOperationException>(() => BlaaizPayoutAmountPolicy.Apply(
            PayoutRequest(100m, null), new BlaaizPayoutRequest()));

    private static RemittancePayoutRequest PayoutRequest(decimal? fromAmount, string? merchantReference) => new(
        TransferId: null, PayoutId: Guid.NewGuid(), PaymentMethod: PaymentMethod.Interac,
        DestinationAmount: 96.5m, SourceCurrencyCode: "CAD", DestinationCurrencyCode: "CAD",
        ProviderCustomerId: "customer", WalletId: "wallet", RecipientFirstName: "Test",
        RecipientLastName: "Recipient", RecipientEmail: "test@example.com", RecipientPhoneNumber: null,
        BankId: null, BankName: null, AccountName: null, AccountNumber: null,
        RoutingNumber: null, SortCode: null, Iban: null, SwiftBic: null, Note: "wallet withdrawal",
        FromAmount: fromAmount, MerchantReference: merchantReference);

    private static FiatWalletWithdrawal Withdrawal() => new()
    {
        Reference = "KXWD-11111111111111111111111111111111",
        CurrencyCode = "NGN", WithdrawalAmount = 100m,
        TotalWalletDebit = 100m, ExpectedRecipientAmount = 96.5m,
        ProviderFee = 3.5m, KorridorXFee = 0m
    };

    private static RemittanceTransactionStatusResult Result(string status) => new(
        ProviderTransactionId: ProviderId, ProviderReference: "blaaiz-ref",
        TransactionType: "payout", ProviderStatus: status,
        CurrencyCode: "NGN", Amount: 96.5m, FailureReason: null,
        ProviderCreatedAt: DateTime.UtcNow, RawResponseJson: "{}",
        ProviderRequestLogId: Guid.NewGuid(), AmountWithoutFee: 96.5m,
        ProviderFeeAmount: 3.5m, ProviderFeeCurrencyCode: "NGN",
        MerchantReference: Withdrawal().Reference);
}
