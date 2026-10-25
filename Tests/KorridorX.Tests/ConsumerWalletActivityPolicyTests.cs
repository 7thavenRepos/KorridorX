using KorridorX.Controllers;
using KorridorX.Models.Enums;
using Xunit;

namespace KorridorX.Tests;

public sealed class ConsumerWalletActivityPolicyTests
{
    [Fact]
    public void Withdrawal_hold_moves_available_into_held_without_reporting_a_completed_debit()
    {
        var (available, held) = WalletActivityDisplayPolicy.BucketDeltas(new[]
        {
            (LedgerBalanceBucket.Available, LedgerPostingSide.Debit, 2000m),
            (LedgerBalanceBucket.Held, LedgerPostingSide.Credit, 2000m)
        });
        Assert.Equal(-2000m, available);
        Assert.Equal(2000m, held);
        Assert.Equal("Verification needed", WalletActivityDisplayPolicy.WithdrawalStatus(
            FiatWalletWithdrawalStatus.RequiresReconciliation));
        Assert.Equal("Withdrawal reserved", WalletActivityDisplayPolicy.Title(
            LedgerTransactionType.Withdrawal, "FiatWalletWithdrawal"));
    }

    [Fact]
    public void Released_hold_restores_available_balance()
    {
        var (available, held) = WalletActivityDisplayPolicy.BucketDeltas(new[]
        {
            (LedgerBalanceBucket.Held, LedgerPostingSide.Debit, 2000m),
            (LedgerBalanceBucket.Available, LedgerPostingSide.Credit, 2000m)
        });
        Assert.Equal(2000m, available);
        Assert.Equal(-2000m, held);
    }

    [Fact]
    public void Capture_reduces_only_held_bucket_not_available_twice()
    {
        var (available, held) = WalletActivityDisplayPolicy.BucketDeltas(new[]
        {
            (LedgerBalanceBucket.Held, LedgerPostingSide.Debit, 150m),
            (LedgerBalanceBucket.External, LedgerPostingSide.Credit, 150m)
        });
        Assert.Equal(0m, available);
        Assert.Equal(-150m, held);
        Assert.Equal("Withdrawal settled", WalletActivityDisplayPolicy.Title(
            LedgerTransactionType.ReservationCapture, "FiatWalletWithdrawal"));
    }
}
