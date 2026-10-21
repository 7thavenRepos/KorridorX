using KorridorX.Models.Payments;
using KorridorX.Providers.Remittance;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.PaymentChannels;

namespace KorridorX.Tests;

public sealed class WalletBankDepositVerificationTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":null}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\",\"account_number\":\"0012345678\"}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\",\"account_number\":null}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\"},\"source_information\":{\"bank_details\":{\"account_number\":\"different-sender-account\"}}}}")]
    public void Matching_or_absent_optional_receiving_metadata_is_accepted_without_using_sender_details(string raw)
    {
        WalletDepositAccountService.ValidateVerifiedDeposit(Account(), Request(), Verified(raw));
    }

    [Theory]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"someone-else\"}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\",\"account_number\":\"other-number\"}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":123}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":null}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"\"}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\",\"account_number\":123}}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":[]}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":\"receiving\"}}")]
    [InlineData("{\"data\":{\"virtual_account_id\":\"wrong-flat-id\",\"virtual_bank_account\":{\"id\":\"receiving\"}}}")]
    public void Explicit_wrong_or_malformed_nested_receiving_identity_is_rejected(string raw)
    {
        Assert.Throws<InvalidOperationException>(() =>
            WalletDepositAccountService.ValidateVerifiedDeposit(Account(), Request(), Verified(raw)));
    }

    [Fact]
    public void Iban_only_account_accepts_explicitly_null_optional_account_number()
    {
        var account = Account();
        account.AssetCode = "EUR";
        account.AccountNumber = null;
        account.Iban = "DE89370400440532013000";
        var request = Request() with { CurrencyCode = "EUR", AccountNumber = null };
        var verified = Verified("{\"data\":{\"virtual_bank_account\":{\"id\":\"receiving\",\"account_number\":null}}}")
            with { CurrencyCode = "EUR" };

        WalletDepositAccountService.ValidateVerifiedDeposit(account, request, verified);
    }

    private static WalletDepositAccount Account() => new()
    {
        Status = "Active", AssetCode = "NGN", ProviderAccountId = "receiving", ProviderCustomerId = "customer",
        ProviderWalletId = "wallet", AccountNumber = "0012345678"
    };

    private static EmbeddedInboundCollectionRequest Request() => new("deposit", null, "SUCCESSFUL", "NGN", 1000m,
        null, null, null, "receiving", "customer", null, "0012345678", "{}", DateTime.UtcNow);

    private static RemittanceTransactionStatusResult Verified(string raw) =>
        new("deposit", null, "collection", "SUCCESSFUL", "NGN", 1000m, null, null, raw, Guid.NewGuid());
}
