using KorridorX.Models.Notifications;

namespace KorridorX.Services.Notifications;

public sealed class RoutingNotificationDeliveryProvider
    : INotificationDeliveryProvider
{
    private readonly SmtpNotificationDeliveryProvider _smtp;
    private readonly FirebasePushNotificationDeliveryProvider _push;

    public RoutingNotificationDeliveryProvider(
        SmtpNotificationDeliveryProvider smtp,
        FirebasePushNotificationDeliveryProvider push)
    {
        _smtp = smtp;
        _push = push;
    }

    public Task<NotificationDeliveryResult> SendAsync(
        NotificationMessage notification,
        CancellationToken ct = default)
    {
        if (string.Equals(
            notification.Channel,
            NotificationChannels.Email,
            StringComparison.OrdinalIgnoreCase))
        {
            return _smtp.SendAsync(notification, ct);
        }

        if (string.Equals(
            notification.Channel,
            NotificationChannels.Push,
            StringComparison.OrdinalIgnoreCase))
        {
            return _push.SendAsync(notification, ct);
        }

        return Task.FromResult(
            new NotificationDeliveryResult(
                false,
                ErrorMessage:
                    $"Unsupported notification channel '{notification.Channel}'.",
                IsPermanentFailure: true));
    }
}
