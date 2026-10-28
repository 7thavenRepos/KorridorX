using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Transfers;

namespace KorridorX.Tests;

public sealed class TransferTimelinePresentationTests
{
    private static readonly DateTime At = new(2026, 10, 28, 22, 42, 20, DateTimeKind.Utc);

    [Fact]
    public void Backdated_provider_times_do_not_move_processing_above_completion_or_rewrite_audit_events()
    {
        var blocked = Row("PAYOUT_DISPATCH_BLOCKED", At.AddHours(-1), At.AddHours(-1));
        blocked.Title = "Transfer needs attention";
        blocked.Description = "Your transfer is waiting for an available payout method.";
        blocked.MetadataJson = """{"supportCode":"KX-PAYOUT-005","stage":"payment_channel"}""";
        var processing = Row("TRANSFER_PROCESSING", At, At);
        var sending = Row("TRANSFER_PAYOUT_INITIATED", At.AddSeconds(1), At.AddSeconds(-1));
        var paid = Row("PAYOUT_COMPLETED", At.AddSeconds(2), At.AddSeconds(-1));
        var completed = Row("TRANSFER_COMPLETED", At.AddSeconds(3), At.AddSeconds(-1));
        var result = TransferTimelinePresentation.Build(
            new[] { processing, paid, blocked, completed, sending }, TransferStatus.Completed);
        Assert.Equal(new[] { completed.Id, paid.Id, sending.Id, processing.Id, blocked.Id },
            result.Select(x => x.Id));
        Assert.Equal(completed.OccurredAt, result[0].OccurredAt);
        Assert.Equal("Earlier delay resolved", result[^1].Title);
        Assert.Equal(blocked.MetadataJson, result[^1].MetadataJson);
        Assert.Equal("Transfer needs attention", blocked.Title);
        Assert.Equal("Your transfer is waiting for an available payout method.", blocked.Description);
    }

    [Fact]
    public void Equal_recording_and_provider_times_use_a_stable_completion_sequence()
    {
        var result = TransferTimelinePresentation.Build(new[] {
            Row("TRANSFER_PROCESSING", At, At), Row("PAYOUT_COMPLETED", At, At),
            Row("TRANSFER_COMPLETED", At, At), Row("PAYOUT_INITIATED", At, At)
        }, TransferStatus.Completed);
        Assert.Equal(new[] { "TRANSFER_COMPLETED", "PAYOUT_COMPLETED", "PAYOUT_INITIATED", "TRANSFER_PROCESSING" },
            result.Select(x => x.EventType));
    }

    [Fact]
    public void An_unresolved_delay_remains_visible_for_a_pending_transfer()
    {
        var blocked = Row("PAYOUT_DISPATCH_BLOCKED", At, At);
        blocked.Title = "Transfer needs attention";
        blocked.Description = "Review the current payout method.";
        var result = Assert.Single(TransferTimelinePresentation.Build(new[] { blocked }, TransferStatus.PaymentReceived));
        Assert.Equal(blocked.Title, result.Title);
        Assert.Equal(blocked.Description, result.Description);
    }

    private static TransferTimelineEvent Row(string type, DateTime recorded, DateTime occurred) =>
        new() { EventType = type, Title = type, CreatedAt = recorded, OccurredAt = occurred };
}
