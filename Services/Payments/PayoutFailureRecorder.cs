using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Remittance.Blaaiz;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Payments;

// A diagnostic event is not a payout, a refund or a status transition. Only
// unsubmitted transfers receive it; a pending/unknown provider attempt must be
// reconciled through the existing flow.
public static class PayoutFailureRecorder
{
    public const string EventType = "PAYOUT_DISPATCH_BLOCKED";

    public static async Task<bool> RecordAsync(AppDbContext db, Guid transferId,
        string stage, string reasonCode, string message, CancellationToken ct)
    {
        if (!await db.Transfers.AsNoTracking().AnyAsync(t => t.Id == transferId && !t.IsDeleted &&
            (t.Status == TransferStatus.PaymentReceived || t.Status == TransferStatus.Processing), ct) ||
            await db.Payouts.AsNoTracking().AnyAsync(p => p.TransferId == transferId &&
                (p.ProviderPayoutId != null || p.ProviderReference != null ||
                 p.Status != PayoutStatus.Pending ||
                 db.PayoutAttempts.Any(a => a.PayoutId == p.Id)), ct))
            return false;

        var safeMessage = BlaaizErrorDetails.Safe(message);
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(stage + "|" + reasonCode + "|" + safeMessage)));
        var latest = await db.TransferTimelineEvents.AsNoTracking()
            .Where(x => x.TransferId == transferId && x.EventType == EventType && !x.IsDeleted)
            .OrderByDescending(x => x.OccurredAt).Select(x => x.MetadataJson).FirstOrDefaultAsync(ct);
        if (latest is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(latest);
                if (document.RootElement.TryGetProperty("fingerprint", out var previous) &&
                    previous.ValueKind == JsonValueKind.String && previous.GetString() == fingerprint)
                    return false;
            }
            catch (JsonException) { }
        }

        var explanation = stage switch
        {
            "provider_customer_compliance" => "Your transfer is waiting for verification.",
            "recipient_destination" or "recipient_details" => "We could not confirm the recipient details.",
            "payment_channel" => "Your transfer is waiting for an available payout method.",
            "provider_liquidity" => "Your transfer is waiting for payout funds to be available.",
            _ => "Your transfer needs a processing review."
        };
        db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transferId, EventType = EventType, Title = "Transfer needs attention",
            Description = explanation + " Contact support with code KX-PAYOUT-005.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                supportCode = "KX-PAYOUT-005", stage, reasonCode, message = safeMessage,
                providerSubmissionRecorded = false, fingerprint
            })
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
