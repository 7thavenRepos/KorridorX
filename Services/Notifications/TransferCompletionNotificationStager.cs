using System.Globalization;
using System.Text.Encodings.Web;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Notifications;
using KorridorX.Models.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

// Durable intent shares the completion save. Only the existing delivery worker
// calls SMTP/Firebase after commit.
public sealed class TransferCompletionNotificationStager(
    AppDbContext db, IOptions<NotificationDeliveryOptions> options)
{
    public const string CompletedSubject = "Your KorridorX transfer is complete";
    public const string EntityType = "Transfer";

    public void Stage(Transfer transfer)
    {
        if (transfer.Status != TransferStatus.Completed || transfer.BusinessProfileId.HasValue ||
            !transfer.CustomerProfileId.HasValue) return;
        var userId = transfer.CustomerProfile?.UserId ?? db.CustomerProfiles.AsNoTracking()
            .Where(x => x.Id == transfer.CustomerProfileId.Value && !x.IsDeleted)
            .Select(x => x.UserId).FirstOrDefault();
        if (userId == Guid.Empty) return;
        var entityId = transfer.Id.ToString();
        bool Matches(NotificationMessage x) => x.UserId == userId && x.RelatedEntityType == EntityType &&
            x.RelatedEntityId == entityId && x.Subject.StartsWith(CompletedSubject, StringComparison.Ordinal);
        if (db.NotificationMessages.Local.Any(Matches) ||
            db.NotificationMessages.IgnoreQueryFilters().AsNoTracking().Any(x =>
                x.UserId == userId && x.RelatedEntityType == EntityType &&
                x.RelatedEntityId == entityId && x.Subject.StartsWith(CompletedSubject))) return;
        var email = db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Email).SingleOrDefault();
        if (string.IsNullOrWhiteSpace(email)) return;
        var receipt = BuildReceipt(transfer);
        var now = DateTime.UtcNow;
        void Add(string channel, string recipient, string subject, string body)
        {
            db.NotificationMessages.Add(new NotificationMessage {
                UserId = userId, Channel = channel, Recipient = recipient, Subject = subject,
                Body = body, RelatedEntityType = EntityType, RelatedEntityId = entityId,
                Status = NotificationStatuses.Pending, AttemptCount = 0,
                MaxAttempts = options.Value.MaxAttempts, NextAttemptAt = now
            });
        }
        // The existing in-app inbox also reads the user's Email queue records.
        Add(NotificationChannels.Email, email.Trim(), receipt.Subject, receipt.Body);
        if (!options.Value.Firebase.IsEnabled) return;
        var devices = db.MobilePushDevices.AsNoTracking().Where(x =>
                x.UserId == userId && x.IsActive && !x.IsDeleted)
            .Select(x => x.PushTokenHash).Distinct().ToList();
        foreach (var device in devices)
            Add(NotificationChannels.Push, device, CompletedSubject,
                "Your transfer is complete. Open KorridorX to view the receipt.");
    }

    public static (string Subject, string Body) BuildReceipt(Transfer transfer)
    {
        if (transfer.Status != TransferStatus.Completed)
            throw new InvalidOperationException("A completion receipt requires a completed transfer.");
        static string E(string value) => HtmlEncoder.Default.Encode(value);
        static string M(decimal value) => value.ToString("#,0.00##########################", CultureInfo.InvariantCulture);
        return ($"{CompletedSubject}: {transfer.Reference}",
            "<p>Your KorridorX transfer has completed successfully.</p>" +
            $"<p><strong>Recipient received: {E(transfer.DestinationCurrencyCode)} {M(transfer.DestinationAmount)}</strong></p>" +
            $"<p>You sent: {E(transfer.SourceCurrencyCode)} {M(transfer.SourceAmount)}</p>" +
            $"<p>Fee: {E(transfer.FeeCurrencyCode)} {M(transfer.FeeAmount)}</p>" +
            $"<p>Total charged: {E(transfer.SourceCurrencyCode)} {M(transfer.TotalPayableAmount)}</p>" +
            $"<p>Reference: {E(transfer.Reference)}</p>" +
            "<p>Open KorridorX to view your transfer receipt and transaction history.</p>");
    }
}
