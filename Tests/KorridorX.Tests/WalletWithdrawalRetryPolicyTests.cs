using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 11, 30, 0, DateTimeKind.Utc);
    private const string Rejection =
        "{\"message\":\"The note must not be greater than 50 characters.\",\"errors\":{\"note\":[\"The note must not be greater than 50 characters.\"]}}";

    private static (FiatWalletWithdrawal Withdrawal, Payout Payout, ProviderRequestLog Log) Valid()
    {
        var id = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var row = new FiatWalletWithdrawal
        {
            Id = id, PayoutId = payoutId, FinancialAccountId = walletId,
            FinancialReservationId = Guid.NewGuid(), PaymentChannelId = channelId,
            Reference = "KXWD-123", CurrencyCode = "NGN",
            PaymentMethod = PaymentMethod.BankTransfer,
            TotalWalletDebit = 2000m, ExpectedRecipientAmount = 1850m,
            Status = FiatWalletWithdrawalStatus.RequiresReconciliation,
            SubmittedToProviderAtUtc = Now.AddHours(-3)
        };
        var payout = new Payout
        {
            Id = payoutId, Reference = row.Reference, FinancialAccountId = walletId,
            RelatedEntityType = nameof(FiatWalletWithdrawal), RelatedEntityId = id,
            Purpose = PaymentOperationPurpose.Withdrawal,
            PaymentChannelId = channelId, PaymentMethod = row.PaymentMethod,
            CurrencyCode = "NGN", Amount = 1850m, ProviderCode = "Blaaiz",
            Status = PayoutStatus.Processing
        };
        payout.Attempts.Add(new PayoutAttempt
        {
            PayoutId = payoutId, Status = ProviderRequestStatus.Pending
        });
        var post = new ProviderRequestLog
        {
            RelatedPayoutId = payoutId, HttpMethod = "POST",
            Status = ProviderRequestStatus.Failed,
            ResponseStatusCode = 422, ResponseBodyJson = Rejection
        };
        return (row, payout, post);
    }

    private static bool Eligible(FiatWalletWithdrawal w, Payout p, ProviderRequestLog l,
        int posts = 1, int notFound = 10, int otherGets = 0, DateTime? last = null) =>
        WalletWithdrawalRetryPolicy.CanRetryOnce(
            w, p, l, posts, notFound, otherGets, last ?? Now.AddMinutes(-5), Now);

    [Fact]
    public void Original_exact_rejection_with_single_claim_and_404s_can_be_manually_retried()
    {
        var (w, p, l) = Valid();
        Assert.True(Eligible(w, p, l));
    }

    [Theory]
    [InlineData(200, "{}")]
    [InlineData(422, "{\"errors\":{\"account_number\":[\"Invalid\"]}}")]
    [InlineData(422, "{\"errors\":{\"note\":[\"Different message\"]}}")]
    [InlineData(422, "{\"errors\":{\"note\":[\"The note must not be greater than 50 characters.\"],\"bank_id\":[\"Missing\"]}}")]
    [InlineData(422, "{invalid}")]
    [InlineData(422, "{\"transaction\":{\"id\":\"payout\"},\"errors\":{\"note\":[\"The note must not be greater than 50 characters.\"]}}")]
    public void No_other_validation_response_is_authorized_for_a_new_post(int status, string body)
    {
        var (w, p, l) = Valid();
        l.ResponseStatusCode = status;
        l.ResponseBodyJson = body;
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void Any_second_post_disables_additional_retry()
    {
        var (w, p, l) = Valid();
        Assert.False(Eligible(w, p, l, posts: 2));
    }

    [Fact]
    public void Any_non404_provider_lookup_disables_retry()
    {
        var (w, p, l) = Valid();
        Assert.False(Eligible(w, p, l, otherGets: 1));
    }

    [Fact]
    public void Missing_404_evidence_disables_retry()
    {
        var (w, p, l) = Valid();
        Assert.False(Eligible(w, p, l, notFound: 0));
    }

    [Fact]
    public void Stale_provider_absence_evidence_disables_retry()
    {
        var (w, p, l) = Valid();
        Assert.False(Eligible(w, p, l, last: Now.AddHours(-25)));
    }

    [Fact]
    public void A_second_durable_attempt_disables_retry()
    {
        var (w, p, l) = Valid();
        p.Attempts.Add(new PayoutAttempt { PayoutId = p.Id, Status = ProviderRequestStatus.Pending });
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void Pinned_provider_identity_disables_retry()
    {
        var (w, p, l) = Valid();
        p.ProviderPayoutId = "already-created";
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void Mismatched_withdrawal_or_amount_disables_retry()
    {
        var (w, p, l) = Valid();
        p.Amount = 1800m;
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void Terminal_withdrawals_cannot_be_requeued()
    {
        var (w, p, l) = Valid();
        w.Status = FiatWalletWithdrawalStatus.FailedReleased;
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void The_audit_log_must_record_a_failed_post()
    {
        var (w, p, l) = Valid();
        l.Status = ProviderRequestStatus.Successful;
        Assert.False(Eligible(w, p, l));
    }

    [Fact]
    public void Only_the_latest_post_controls_automated_lookup_pause()
    {
        Assert.True(WalletWithdrawalRetryPolicy.IsLatestKnownNoteRejection(422, Rejection));
        Assert.False(WalletWithdrawalRetryPolicy.IsLatestKnownNoteRejection(200, "{}"));
        Assert.False(WalletWithdrawalRetryPolicy.IsLatestKnownNoteRejection(422, "{\"errors\":{}}"));
    }
}