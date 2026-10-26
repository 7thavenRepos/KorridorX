using KorridorX.Models.AccountClosure;
using KorridorX.Models.Enums;
using KorridorX.Services.AccountClosure;
using Xunit;

namespace KorridorX.Tests;

public sealed class AccountClosurePolicyTests
{
    [Fact]
    public void Request_is_never_equivalent_to_account_deletion()
    {
        var request = new AccountClosureRequest { Status = AccountClosureRequestStatus.Requested };
        Assert.True(AccountClosurePolicy.IsOpen(request.Status));
        Assert.Null(request.CompletedAtUtc);
        Assert.False(AccountClosurePolicy.IsOpen(AccountClosureRequestStatus.Withdrawn));
        Assert.False(AccountClosurePolicy.IsOpen(AccountClosureRequestStatus.Completed));
    }

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(100, 0, 0, true)]
    [InlineData(0, 10, 0, true)]
    [InlineData(0, 0, 10, true)]
    [InlineData(-1, 0, 0, true)]
    public void Any_remaining_wallet_balance_requires_review(decimal settled, decimal available,
        decimal held, bool hasExposure) =>
        Assert.Equal(hasExposure, AccountClosurePolicy.HasFinancialExposure(settled, available, held));

    [Theory]
    [InlineData(TransferStatus.PendingPayment, true)]
    [InlineData(TransferStatus.PaymentReceived, true)]
    [InlineData(TransferStatus.Processing, true)]
    [InlineData(TransferStatus.PayoutInitiated, true)]
    [InlineData(TransferStatus.RefundPending, true)]
    [InlineData(TransferStatus.Completed, false)]
    [InlineData(TransferStatus.Cancelled, false)]
    public void Unresolved_transfers_are_never_considered_closure_ready(TransferStatus status, bool blocked) =>
        Assert.Equal(blocked, AccountClosurePolicy.IsUnresolvedTransfer(status));

    [Fact]
    public void Held_or_uncertain_withdrawals_block_closure()
    {
        Assert.True(AccountClosurePolicy.IsUnresolvedWithdrawal(FiatWalletWithdrawalStatus.Processing));
        Assert.True(AccountClosurePolicy.IsUnresolvedWithdrawal(FiatWalletWithdrawalStatus.RequiresReconciliation));
        Assert.False(AccountClosurePolicy.IsUnresolvedWithdrawal(FiatWalletWithdrawalStatus.Succeeded));
        Assert.False(AccountClosurePolicy.IsUnresolvedWithdrawal(FiatWalletWithdrawalStatus.FailedReleased));
    }

    [Fact]
    public void Unknown_payouts_and_refunds_require_review()
    {
        Assert.True(AccountClosurePolicy.IsUnresolvedPayout(PayoutStatus.Processing));
        Assert.True(AccountClosurePolicy.IsUnresolvedCollection(CollectionStatus.RefundPending));
        Assert.False(AccountClosurePolicy.IsUnresolvedPayout(PayoutStatus.Successful));
    }
}
