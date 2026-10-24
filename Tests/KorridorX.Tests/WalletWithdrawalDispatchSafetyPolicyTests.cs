using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalDispatchSafetyPolicyTests
{
    [Fact]
    public void Processing_remains_disabled_when_only_initiation_is_enabled()
    {
        var flags = new WalletWithdrawalProcessingOptions { Enabled = true };
        var provider = Provider();
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Development", flags, provider));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanRecover("Development", flags, provider));
    }

    [Fact]
    public void Configured_staging_and_production_do_not_require_sandbox_allowlists()
    {
        var flags = Flags();
        flags.SandboxAllowedWithdrawalIds.Clear();
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Staging", flags, Provider()));
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Production", flags,
            Provider("https://api-prod.blaaiz.com")));
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanRecover("Production", flags,
            Provider("https://api-prod.blaaiz.com")));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Production", flags, Provider()));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Development", flags,
            Provider("https://api-prod.blaaiz.com")));
    }

    [Fact]
    public void Sandbox_dispatch_requires_exact_withdrawal_id_and_explicit_currency_debit_ceiling()
    {
        var row = Withdrawal();
        var flags = Flags(row.Id);
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));

        flags.SandboxMaxNgnDebit = 0m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        flags.SandboxMaxNgnDebit = 99.99m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        flags.SandboxMaxNgnDebit = WalletWithdrawalDispatchSafetyPolicy.AbsoluteMaxNgnDebit + 0.01m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        flags.SandboxMaxNgnDebit = 100m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(
            new FiatWalletWithdrawal { CurrencyCode = "NGN", TotalWalletDebit = 1m }, flags));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsSandboxAllowlisted(Guid.Empty, flags));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsSandboxAllowlisted(Guid.NewGuid(), flags));
    }

    [Fact]
    public void Cad_uses_independent_small_debit_ceiling_and_other_fiat_currencies_remain_ineligible()
    {
        var row = Withdrawal();
        row.CurrencyCode = "CAD";
        var flags = Flags(row.Id);
        flags.SandboxMaxCadDebit = 0m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        flags.SandboxMaxCadDebit = 100m;
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        flags.SandboxMaxCadDebit = 101m;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
        row.CurrencyCode = "USD";
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.IsWithinSandboxDebitCeiling(row, flags));
    }

    [Theory]
    [InlineData("Production", "https://api-dev.blaaiz.com")]
    [InlineData("Staging", "https://api.blaaiz.com")]
    [InlineData("Development", "https://api.blaaiz.com")]
    [InlineData("Development", "http://api-dev.blaaiz.com")]
    [InlineData("Development", "https://api-dev.blaaiz.com.malicious.example")]
    [InlineData("development", "https://api-dev.blaaiz.com")]
    public void Dispatch_cannot_target_non_development_host_or_environment(string environment, string url)
    {
        var flags = Flags();
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanDispatch(environment, flags, Provider(url)));
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanRecover(environment, flags, Provider(url)));
    }

    [Fact]
    public void Explicit_development_dispatch_and_recovery_flags_are_independent()
    {
        var flags = Flags();
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Development", flags, Provider()));
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanRecover("Development", flags, Provider()));
        flags.DispatchEnabled = false;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanDispatch("Development", flags, Provider()));
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanRecover("Development", flags, Provider()));
        flags.RecoveryEnabled = false;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanRecover("Development", flags, Provider()));
    }

    [Theory]
    [InlineData(FiatWalletWithdrawalStatus.Processing)]
    [InlineData(FiatWalletWithdrawalStatus.Succeeded)]
    [InlineData(FiatWalletWithdrawalStatus.FailedReleased)]
    [InlineData(FiatWalletWithdrawalStatus.RequiresReconciliation)]
    [InlineData(FiatWalletWithdrawalStatus.CancelledReleased)]
    public void A_claimed_or_terminal_withdrawal_cannot_be_submitted_again(FiatWalletWithdrawalStatus status)
    {
        var row = Withdrawal(); row.Status = status;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanClaim(row));
    }

    [Fact]
    public void Claimed_payout_id_blocks_duplicate_POST_even_if_status_was_reverted()
    {
        var row = Withdrawal(); row.PayoutId = Guid.NewGuid();
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanClaim(row));
        row.PayoutId = null; row.OperationalHoldReason = "operations review";
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanClaim(row));
        row.OperationalHoldReason = null;
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanClaim(row));
    }

    [Fact]
    public void Cancellation_is_allowed_only_before_a_durable_provider_claim()
    {
        var row = Withdrawal();
        Assert.True(WalletWithdrawalDispatchSafetyPolicy.CanCancelUnsubmitted(row));
        row.SubmittedToProviderAtUtc = DateTime.UtcNow;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanCancelUnsubmitted(row));
        row.SubmittedToProviderAtUtc = null; row.PayoutId = Guid.NewGuid();
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanCancelUnsubmitted(row));
        row.PayoutId = null; row.Status = FiatWalletWithdrawalStatus.RequiresReconciliation;
        Assert.False(WalletWithdrawalDispatchSafetyPolicy.CanCancelUnsubmitted(row));
    }

    [Fact]
    public void Ng_wallet_withdrawal_is_inclusive_and_uses_saved_bank_details()
    {
        var row = Withdrawal();
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(row,
            Guid.NewGuid(), "customer", "wallet", "0123456789");
        Assert.Equal(100m, request.FromAmount);
        Assert.Equal(96.5m, request.DestinationAmount);
        Assert.Equal(row.Reference, request.MerchantReference);
        Assert.Equal("bank-id", request.BankId);
        Assert.Equal("0123456789", request.AccountNumber);
        Assert.Equal(PaymentMethod.BankTransfer, request.PaymentMethod);
        Assert.Null(request.TransferId);
    }

    [Fact]
    public void Cad_wallet_withdrawal_is_inclusive_and_uses_confirmed_owner_email()
    {
        var row = Withdrawal();
        row.CurrencyCode = "CAD"; row.PaymentMethod = PaymentMethod.Interac;
        row.ProviderBankId = null; row.ProtectedAccountNumber = null;
        row.RecipientEmail = "owner@example.com";
        var request = WalletWithdrawalDispatchSafetyPolicy.BuildRequest(row,
            Guid.NewGuid(), "customer", "wallet", null);
        Assert.Equal("CAD", request.SourceCurrencyCode);
        Assert.Equal("owner@example.com", request.RecipientEmail);
        Assert.Null(request.AccountNumber);
        Assert.Equal(row.Reference, request.MerchantReference);
    }

    [Theory]
    [InlineData("USD", PaymentMethod.BankTransfer)]
    [InlineData("EUR", PaymentMethod.BankTransfer)]
    [InlineData("GBP", PaymentMethod.BankTransfer)]
    [InlineData("NGN", PaymentMethod.Interac)]
    [InlineData("CAD", PaymentMethod.BankTransfer)]
    public void Unsupported_or_incomplete_currency_and_method_pairs_must_not_dispatch(string currency, PaymentMethod method)
    {
        var row = Withdrawal(); row.CurrencyCode = currency; row.PaymentMethod = method;
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalDispatchSafetyPolicy.BuildRequest(row, Guid.NewGuid(), "customer", "wallet", "0123456789"));
    }

    [Theory]
    [InlineData("missingReserve")]
    [InlineData("unexpectedFee")]
    [InlineData("wrongTotal")]
    [InlineData("zeroNet")]
    [InlineData("unknownPolicy")]
    [InlineData("missingBank")]
    public void Broken_financial_or_destination_snapshot_cannot_dispatch(string variant)
    {
        var row = Withdrawal();
        switch (variant)
        {
            case "missingReserve": row.FinancialReservationId = null; break;
            case "unexpectedFee": row.KorridorXFee = 1m; break;
            case "wrongTotal": row.TotalWalletDebit = 101m; break;
            case "zeroNet": row.ExpectedRecipientAmount = 0m; break;
            case "unknownPolicy": row.KorridorXFeePolicy = "CUSTOM"; break;
            case "missingBank": row.ProtectedAccountNumber = null; break;
        }
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalDispatchSafetyPolicy.BuildRequest(row, Guid.NewGuid(), "customer", "wallet", "0123456789"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789A")]
    [InlineData("")]
    public void Ng_payout_rejects_invalid_decrypted_bank_numbers(string bank)
    {
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalDispatchSafetyPolicy.BuildRequest(Withdrawal(),
                Guid.NewGuid(), "customer", "wallet", bank));
    }

    private static WalletWithdrawalProcessingOptions Flags() => new()
    {
        Enabled = true, DispatchEnabled = true, RecoveryEnabled = true,
        SandboxAllowedWithdrawalIds = [Guid.NewGuid()],
        SandboxMaxNgnDebit = 100m, SandboxMaxCadDebit = 100m
    };

    private static WalletWithdrawalProcessingOptions Flags(Guid withdrawalId)
    {
        var flags = Flags();
        flags.SandboxAllowedWithdrawalIds = [withdrawalId];
        return flags;
    }

    private static BlaaizOptions Provider(string url = "https://api-dev.blaaiz.com") => new()
    {
        IsEnabled = true, BaseUrl = url
    };

    private static FiatWalletWithdrawal Withdrawal() => new()
    {
        Reference = "KXWD-test1234567890", CurrencyCode = "NGN", SourceCountryCode = "NG",
        PaymentMethod = PaymentMethod.BankTransfer, WithdrawalAmount = 100m,
        ExpectedRecipientAmount = 96.5m, ProviderFee = 3.5m,
        KorridorXFee = 0m, KorridorXFeePolicy = "NOT_CONFIGURED",
        TotalWalletDebit = 100m, FinancialReservationId = Guid.NewGuid(),
        ProviderBankId = "bank-id", ProtectedAccountNumber = "ciphertext",
        VerifiedAccountName = "Test Recipient"
    };
}
