using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

public sealed class FirebasePushNotificationDeliveryProvider
{
    private readonly AppDbContext _db;
    private readonly NotificationDeliveryOptions _options;
    private readonly IFirebasePushSender _sender;
    private readonly ILogger<FirebasePushNotificationDeliveryProvider> _logger;

    public FirebasePushNotificationDeliveryProvider(
        AppDbContext db,
        IOptions<NotificationDeliveryOptions> options,
        IFirebasePushSender sender,
        ILogger<FirebasePushNotificationDeliveryProvider> logger)
    {
        _db = db;
        _options = options.Value;
        _sender = sender;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationMessage notification,
        CancellationToken ct = default)
    {
        if (!_options.Firebase.IsEnabled)
        {
            return new NotificationDeliveryResult(
                false,
                ErrorMessage: "Firebase push delivery is disabled.",
                IsCancelled: true);
        }

        var device = await _db.MobilePushDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    !x.IsDeleted &&
                    x.IsActive &&
                    x.PushTokenHash == notification.Recipient,
                ct);

        if (device is null)
        {
            return new NotificationDeliveryResult(
                false,
                ErrorMessage: "The registered push device is no longer active.",
                IsCancelled: true);
        }

        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["route"] = ResolveRoute(notification)
        };

        if (!string.IsNullOrWhiteSpace(notification.RelatedEntityType))
            data["relatedEntityType"] = notification.RelatedEntityType;

        if (!string.IsNullOrWhiteSpace(notification.RelatedEntityId))
            data["relatedEntityId"] = notification.RelatedEntityId;

#pragma warning disable CS0618
        var message = new Message
        {
            Token = device.PushToken,
            Notification = new Notification
            {
                Title = notification.Subject,
                Body = notification.Body
            },
            Data = data,
            Android = new AndroidConfig
            {
                Priority = Priority.High
            }
        };
#pragma warning restore CS0618

        try
        {
            var providerMessageId = await _sender.SendAsync(message, ct);

            return new NotificationDeliveryResult(
                true,
                ProviderMessageId: providerMessageId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (FirebaseMessagingException ex)
        {
            var messagingCode = ex.MessagingErrorCode;
            var isUnregistered =
                messagingCode == MessagingErrorCode.Unregistered;

            var isTransient =
                messagingCode is MessagingErrorCode.Internal
                    or MessagingErrorCode.QuotaExceeded
                    or MessagingErrorCode.Unavailable ||
                ex.ErrorCode is ErrorCode.Internal
                    or ErrorCode.ResourceExhausted
                    or ErrorCode.Unavailable
                    or ErrorCode.DeadlineExceeded
                    or ErrorCode.Unknown;

            var isPermanent =
                !isTransient &&
                messagingCode is MessagingErrorCode.InvalidArgument
                    or MessagingErrorCode.SenderIdMismatch
                    or MessagingErrorCode.ThirdPartyAuthError
                    or MessagingErrorCode.Unregistered;

            _logger.LogWarning(
                "Firebase push delivery failed for notification {NotificationId}. MessagingError={MessagingError}; ErrorCode={ErrorCode}; Permanent={Permanent}.",
                notification.Id,
                messagingCode,
                ex.ErrorCode,
                isPermanent);

            return new NotificationDeliveryResult(
                false,
                ErrorMessage: Truncate(ex.Message, 1000),
                IsPermanentFailure: isPermanent,
                InvalidPushTokenHash:
                    isUnregistered ? notification.Recipient : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Firebase push delivery failed for notification {NotificationId}.",
                notification.Id);

            return new NotificationDeliveryResult(
                false,
                ErrorMessage: Truncate(ex.Message, 1000));
        }
    }

    private static string ResolveRoute(NotificationMessage notification)
    {
        if (string.Equals(
            notification.RelatedEntityType,
            "Transfer",
            StringComparison.OrdinalIgnoreCase))
        {
            return "transfer";
        }

        if (string.Equals(
            notification.RelatedEntityType,
            "SupportTicket",
            StringComparison.OrdinalIgnoreCase))
        {
            return "support-ticket";
        }

        if (string.Equals(
            notification.RelatedEntityType,
            "TransferDispute",
            StringComparison.OrdinalIgnoreCase))
        {
            return "support";
        }

        return "notifications";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
