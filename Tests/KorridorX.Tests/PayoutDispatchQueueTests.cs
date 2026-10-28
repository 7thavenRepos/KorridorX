using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class PayoutDispatchQueueTests
{
    [Theory]
    [InlineData(TransferStatus.PaymentReceived, true)]
    [InlineData(TransferStatus.Processing, true)]
    [InlineData(TransferStatus.PendingPayment, false)]
    [InlineData(TransferStatus.Cancelled, false)]
    [InlineData(TransferStatus.Completed, false)]
    [InlineData(TransferStatus.RefundPending, false)]
    public void Only_funded_unsubmitted_statuses_enter_queue(TransferStatus status, bool expected)
    {
        var transfer = new Transfer { Status = status };
        Assert.Equal(expected, Eligible(transfer, null));
    }

    [Fact]
    public void Pending_shell_resumes_but_any_attempt_or_provider_identity_blocks_automatic_resubmission()
    {
        var transfer = new Transfer { Status = TransferStatus.PaymentReceived };
        var payout = new Payout { TransferId = transfer.Id, Status = PayoutStatus.Pending };
        Assert.True(Eligible(transfer, payout));
        foreach (var status in Enum.GetValues<ProviderRequestStatus>())
        {
            Assert.False(Eligible(transfer, payout,
                new PayoutAttempt { PayoutId = payout.Id, Status = status }));
        }
        Assert.False(Eligible(transfer, payout,
            new PayoutAttempt { PayoutId = payout.Id, IsDeleted = true }));
        payout.ProviderPayoutId = "known-provider-payment";
        Assert.False(Eligible(transfer, payout));
        payout.ProviderPayoutId = null;
        payout.ProviderReference = "known-provider-reference";
        Assert.False(Eligible(transfer, payout));
        payout.ProviderReference = null;
        payout.IsDeleted = true;
        Assert.False(Eligible(transfer, payout));
        payout.IsDeleted = false;
        transfer.ProviderTransferId = "known-provider-payment";
        Assert.False(Eligible(transfer, payout));
    }

    [Theory]
    [InlineData(PayoutStatus.Initiated)]
    [InlineData(PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Successful)]
    [InlineData(PayoutStatus.Failed)]
    [InlineData(PayoutStatus.Reversed)]
    public void A_terminal_or_started_payout_requires_reconciliation(PayoutStatus status)
    {
        var transfer = new Transfer { Status = TransferStatus.PaymentReceived };
        Assert.False(Eligible(transfer, new Payout { TransferId = transfer.Id, Status = status }));
    }

    [Fact]
    public void Queue_translates_to_PostgreSQL_without_opening_a_connection()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=query_translation_only").Options;
        using var db = new AppDbContext(options);
        var query = PayoutDispatchQueue.Unsubmitted(db.Transfers, db.Payouts, db.PayoutAttempts)
            .Where(x => !x.IsComplianceHold && !x.IsOperationalHold);
        var sql = query.ToQueryString();
        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PayoutAttempts", sql);
        Assert.Contains("ProviderPayoutId", sql);
    }

    private static bool Eligible(Transfer transfer, Payout? payout, params PayoutAttempt[] attempts) =>
        PayoutDispatchQueue.Unsubmitted(new[] { transfer }.AsQueryable(),
            (payout is null ? Array.Empty<Payout>() : new[] { payout }).AsQueryable(),
            attempts.AsQueryable()).Any();
}
