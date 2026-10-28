using KorridorX.Dtos.Transfers;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Transfers;

/// <summary>Read-only customer presentation; recorded audit events are unchanged.</summary>
public static class TransferTimelinePresentation
{
    public static List<TransferTimelineEventDto> Build(
        IEnumerable<TransferTimelineEvent> events, TransferStatus currentStatus)
    {
        // Provider clocks can precede local processing, and one provider response
        // can supply the same OccurredAt for several successive transitions.
        // CreatedAt records when KorridorX actually added each transition.
        var ordered = events.OrderByDescending(RecordedAt)
            .ThenByDescending(x => Milestone(x.EventType))
            .ThenByDescending(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .ToList();
        var completion = ordered.FirstOrDefault(x => Is(x.EventType, "TRANSFER_COMPLETED"));
        return ordered.Select(x =>
        {
            var resolved = currentStatus == TransferStatus.Completed && completion is not null &&
                Is(x.EventType, "PAYOUT_DISPATCH_BLOCKED") && RecordedAt(x) <= RecordedAt(completion);
            return new TransferTimelineEventDto(x.Id, x.TransferId, x.EventType,
                resolved ? "Earlier delay resolved" : x.Title,
                resolved ? "The earlier payout delay was resolved and this transfer has completed." : x.Description,
                x.MetadataJson, x.OccurredAt);
        }).ToList();
    }

    private static DateTime RecordedAt(TransferTimelineEvent row) =>
        row.CreatedAt == default ? row.OccurredAt : row.CreatedAt;

    private static bool Is(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    // Only resolves equal recording timestamps; never groups separate retries
    // or later failures ahead of their actual recording order.
    private static int Milestone(string type) => type.ToUpperInvariant() switch
    {
        "TRANSFER_DRAFTED" or "TRANSFER_CREATED" => 10,
        "TRANSFER_QUOTED" => 20,
        "TRANSFER_PENDING_PAYMENT" => 30,
        "TRANSFER_FUNDED" or "TRANSFER_PAYMENT_RECEIVED" => 40,
        "PAYOUT_DISPATCH_BLOCKED" => 45,
        "TRANSFER_PROCESSING" => 50,
        "PAYOUT_INITIATED" or "TRANSFER_PAYOUT_INITIATED" => 60,
        "PAYOUT_COMPLETED" or "TRANSFER_PAYOUT_COMPLETED" => 70,
        "TRANSFER_COMPLETED" => 80,
        _ => 0
    };
}
